using System.Text.Json;
using ErrorOr;
using Kuroe.Configuration.Files;
using Kuroe.Flow.NodeType.Container;
using Kuroe.Flow.NodeLibrary;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Kuroe.Flow.Compilation.Shared;
using Kuroe.Flow.Json;
using Kuroe.Node.Llm;
using Kuroe.Workflows.Flows;
using Xunit;


namespace Kuroe.Tests;

/// <summary>节点库与装配引用：定义复用、端口绑定、实例命名与保存往返。</summary>
public sealed class NodeLibraryTests
{


    [Fact]
    public void Flow_references_library_leaf_and_container_with_port_binding()
    {
        using TestEnvironment environment = TestEnvironment.Create(LibraryFlow);

        FlowDefinition flow = environment.Flows.Find(new FlowName("默认")).ThrowIfError();

        // 展开后：根容器包住制定计划与交付容器，容器成员名带实例前缀，@计划 绑定为制定计划
        NodeSpec root = flow.RootNode;
        Assert.NotNull(root.Nodes);
        Assert.Equal(2, root.Nodes.Count);
        NodeSpec plan = root.Nodes[0];
        Assert.NotNull(plan.Spec);
        Assert.Equal(LlmNodeTypes.Name, plan.Kind);
        Assert.Equal("Start@目标", Assert.Single(Assert.Single(plan.From).Links).Ref.Display);

        NodeSpec deliver = root.Nodes[1];
        Assert.NotNull(deliver.Nodes);
        NodeSpec implement = deliver.Nodes[0];
        Assert.Equal(new NodeLabel("实施"), implement.Label);
        Assert.NotNull(implement.Spec);
        Assert.Equal("制定计划@计划", Assert.Single(Assert.Single(implement.From).Links).Ref.Display);
    }

    [Fact]
    public void Same_library_container_instantiated_twice_keeps_members_distinct()
    {
        using TestEnvironment environment = TestEnvironment.Create(DualInstanceFlow);

        TaskSnapshot done = environment.Settle(environment.Submit("补齐 README", new FlowName("双线")));

        Assert.Equal(TaskState.Done, done.State);
    }

    [Fact]
    public void Container_model_slot_bound_to_distinct_models_per_flow()
    {
        using TestEnvironment environment = TestEnvironment.Create(PerFlowModelBinding);

        CompiledFlow first = environment.Flows.FindCompiled(new FlowName("甲")).ThrowIfError();
        ExecutableNode firstImplement = first.Graph.ExecutableNodes.Single(node => node.Label.Value == "实施");
        Assert.Equal("执行者", StaticText(first, firstImplement, LlmNodeTypes.ModelPort));

        CompiledFlow second = environment.Flows.FindCompiled(new FlowName("乙")).ThrowIfError();
        ExecutableNode secondImplement = second.Graph.ExecutableNodes.Single(node => node.Label.Value == "实施");
        Assert.Equal("实施者", StaticText(second, secondImplement, LlmNodeTypes.ModelPort));
    }

    /// <summary>按编译视图取执行节点静态端口的值，没有开始端口值。</summary>
    private static string? StaticText(CompiledFlow flow, ExecutableNode node, PortName port) =>
        new StaticPortValues(flow.Graph, PortTexts.Empty).For(node).Text(port);

    [Fact]
    public void Unknown_reference_reports_the_missing_definition()
    {
        ErrorOr<TestEnvironment> environment = TestEnvironment.TryCreate(UnknownReferenceWithoutStatic);

        Assert.True(environment.IsError);
        Assert.Contains(environment.ErrorsOrEmptyList, error => error.Description.Contains("不在节点库"));
    }

    [Fact]
    public void Import_keeps_library_and_reference_shape_on_save()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        File.WriteAllText(Path.Combine(environment.Root, "library.json"), TestFlowDocument.Normalize(LibraryFlow));

        FlowImport imported = environment.Flows.Import("library.json").ThrowIfError();
        Assert.Contains(imported.Notes, note => note.Contains("节点库"));

        string text = File.ReadAllText(KuroePaths.At(environment.Root).FlowsFile);
        Assert.Contains("\"Nodes\"", text);
        Assert.Contains("\"Use\"", text);
        Assert.Contains("\"In\"", text);
        Assert.Contains("\"Inputs\"", text);
        Assert.Contains("\"Static\"", text);
        Assert.DoesNotContain("\"Models\"", text);
        Assert.DoesNotContain("\"ModelSlots\"", text);

        // 保存后容器定义写静态输入端口，引用处写 In 绑定
        using JsonDocument saved = JsonDocument.Parse(text);
        JsonElement libraryContainer = saved.RootElement.GetProperty("Nodes")
            .EnumerateArray()
            .Single(node => node.GetProperty("Name").GetString() == "交付");
        JsonElement modelInput = libraryContainer.GetProperty("Inputs")
            .EnumerateArray()
            .Single(input => input.GetProperty("Name").GetString() == "MODEL_NAME");
        Assert.True(modelInput.GetProperty("Static").GetBoolean());

        JsonElement reference = saved.RootElement.GetProperty("Flows")[0].GetProperty("Nodes")[0].GetProperty("Nodes")[1];
        Assert.Equal("@MODEL_NAME", reference.GetProperty("In").GetProperty("MODEL_NAME").GetString());
    }

    /// <summary>文件定义自定义节点时，流程仍可直接引用内置节点；落盘只写文件来源的节点定义。</summary>
    [Fact]
    public void Builtin_node_stays_usable_and_unwritten_when_file_defines_its_own()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        File.WriteAllText(Path.Combine(environment.Root, "builtin.json"), TestFlowDocument.Normalize(BuiltinReferenceFlow));

        environment.Flows.Import("builtin.json").ThrowIfError();

        NodeSpec container = environment.Flows.Find(new FlowName("内置引用")).ThrowIfError().RootNode.Nodes![1];
        Assert.NotNull(container.Nodes);
        NodeSpec implement = container.Nodes[0];
        Assert.NotNull(implement.Spec);
        Assert.Equal(new NodeLabel("实施"), implement.Label);

        FlowDocument document = new JsonFlowFormat()
            .Read(File.ReadAllText(KuroePaths.At(environment.Root).FlowsFile)).ThrowIfError();
        IReadOnlyList<NodeLabel> savedNodes = [.. document.Nodes.Select(node => new NodeLabel(node.Name))];
        Assert.Equal([new NodeLabel("准备")], savedNodes);
    }

    /// <summary>库容器内成员的上游接线条目必须落在子树或声明端口内，环外成员不可见。</summary>
    [Fact]
    public void Member_from_outside_container_scope_is_rejected()
    {
        ErrorOr<TestEnvironment> environment = TestEnvironment.TryCreate(MemberFromOutOfScope);

        Assert.True(environment.IsError);
        Assert.Contains(environment.ErrorsOrEmptyList,
            error => error.Description.Contains("From 引用的节点 外来 不在容器 交付 的作用域里"));
    }

    [Fact]
    public void Import_rejects_duplicated_flow_names()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        File.WriteAllText(Path.Combine(environment.Root, "duplicated.json"), TestFlowDocument.Normalize(DuplicateImportedFlows));

        ErrorOr<FlowImport> imported = environment.Flows.Import("duplicated.json");

        Assert.True(imported.IsError);
        Assert.Contains(imported.ErrorsOrEmptyList, error => error.Description.Contains("流程名重复"));
    }

    /// <summary>库执行定义的次数上限非正，未经引用也在库校验阶段拒绝。</summary>
    [Fact]
    public void Library_executable_with_non_positive_max_executions_is_rejected() =>
        Assert.Equal(
            ["节点库定义 干活库：MaxExecutions 必须是正整数。"],
            LibraryErrors(new NodeSpec { Name = new NodeName("干活库"), Label = new NodeLabel("干活库"), Type = LlmNodeTypes.Create(null), Spec = new ExecutableSpec { MaxExecutions = 0 } }));

    /// <summary>库容器定义的非正统一上限，未经引用也在库校验阶段拒绝。</summary>
    [Fact]
    public void Library_container_with_non_positive_max_executions_is_rejected() =>
        Assert.Equal(
            ["节点库定义 干活组：MaxExecutions 必须是正整数。"],
            LibraryErrors(new NodeSpec
            {
                Name = new NodeName("干活组"),
                Label = new NodeLabel("干活组"),
                Type = ContainerNodeTypes.Type,
                MaxExecutions = 0,
                Nodes = [new NodeSpec { Name = new NodeName("干活"), Label = new NodeLabel("干活"), Type = LlmNodeTypes.Create(null), Inputs = [new InputPort(LlmNodeTypes.ModelPort, PortTypes.Text, null, "执行者", isStatic: true)], Spec = new ExecutableSpec() }],
            }));

    /// <summary>库容器定义必须声明子节点，缺子节点在库校验阶段拒绝。</summary>
    [Fact]
    public void Library_container_without_children_is_rejected() =>
        Assert.Equal(
            ["节点库定义 空组：容器节点必须声明子节点。"],
            LibraryErrors(new NodeSpec { Name = new NodeName("空组"), Label = new NodeLabel("空组"), Type = ContainerNodeTypes.Type }));

    /// <summary>库执行定义不能声明子节点，子节点只属于容器。</summary>
    [Fact]
    public void Library_executable_with_children_is_rejected() =>
        Assert.Equal(
            ["节点库定义 干活库：执行节点与输入节点不能声明子节点，子节点只属于容器。"],
            LibraryErrors(new NodeSpec
            {
                Name = new NodeName("干活库"),
                Label = new NodeLabel("干活库"),
                Type = LlmNodeTypes.Create(null),
                Spec = new ExecutableSpec(),
                Nodes = [new NodeSpec { Name = new NodeName("干活"), Label = new NodeLabel("干活"), Type = LlmNodeTypes.Create(null), Spec = new ExecutableSpec() }],
            }));

    /// <summary>库容器定义的输出必须有来源，且不能声明输出校验。</summary>
    [Fact]
    public void Library_container_with_execution_fields_is_rejected() =>
        Assert.Equal(
            [
                "节点库定义 干活组：库容器定义不声明 Validate，输出校验只属于执行节点。",
                "节点库定义 干活组：输出端口 结论 必须写 Value 或 Ref。",
            ],
            LibraryErrors(new NodeSpec
            {
                Name = new NodeName("干活组"),
                Label = new NodeLabel("干活组"),
                Type = ContainerNodeTypes.Type,
                Outputs = [new Port(new PortName("结论"), PortTypes.Text)],
                Validate = new OutputValidation(ValidationPredicate.NonEmpty, null),
                Nodes = [new NodeSpec { Name = new NodeName("干活"), Label = new NodeLabel("干活"), Type = LlmNodeTypes.Create(null), Inputs = [new InputPort(LlmNodeTypes.ModelPort, PortTypes.Text, null, "执行者", isStatic: true)], Spec = new ExecutableSpec() }],
            }));

    /// <summary>流程名与库作用域名相同时错误仍按流程作用域给出。</summary>
    [Fact]
    public void Flow_named_like_library_scope_keeps_flow_errors()
    {
        ErrorOr<TestEnvironment> created = TestEnvironment.TryCreate(FlowNamedLikeLibraryScope);

        Assert.NotEmpty(created.ErrorsOrEmptyList);
        Assert.All(created.ErrorsOrEmptyList,
            error => Assert.StartsWith("流程 节点库", error.Description));
    }

    /// <summary>导入的文件要能独立装配：流程引用的节点库定义不在同一文件里时不生效。</summary>
    [Fact]
    public void Import_rejects_flow_referencing_library_node_outside_the_file()
    {
        using TestEnvironment environment = TestEnvironment.Create(LibraryFlow);
        File.WriteAllText(Path.Combine(environment.Root, "reuse.json"), TestFlowDocument.Normalize(LibraryReferenceOnlyFlow));

        ErrorOr<FlowImport> imported = environment.Flows.Import("reuse.json");

        Assert.Equal(
            [
                "流程 复用 的节点 干活：引用的节点 外部库 不在节点库。",
                "导入的文件必须能独立装配：流程引用的节点要用内置节点或写在同一文件里。",
            ],
            imported.ErrorsOrEmptyList.Select(error => error.Description));
    }

    /// <summary>导入新名字的流程时逐条说明新增。</summary>
    [Fact]
    public void Import_reports_new_flow()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        File.WriteAllText(Path.Combine(environment.Root, "extra.json"), TestFlowDocument.Normalize(ExtraFlow));

        FlowImport imported = environment.Flows.Import("extra.json").ThrowIfError();

        Assert.Equal(["新增流程 额外"], imported.Notes);
    }

    /// <summary>导入与现有流程同名的流程时逐条说明覆盖。</summary>
    [Fact]
    public void Import_reports_overwritten_flow()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        File.WriteAllText(Path.Combine(environment.Root, "extra.json"), TestFlowDocument.Normalize(OverwritingFlow));

        FlowImport imported = environment.Flows.Import("extra.json").ThrowIfError();

        Assert.Equal(["覆盖流程 默认"], imported.Notes);
    }

    /// <summary>文件定义的节点与内置节点同名时覆盖内置定义。</summary>
    [Fact]
    public void File_node_overrides_builtin_definition()
    {
        using TestEnvironment environment = TestEnvironment.Create(OverridingBuiltinFlow);

        FlowDefinition flow = environment.Flows.Find(new FlowName("默认")).ThrowIfError();
        NodeSpec implement = flow.RootNode.Nodes![0];

        Assert.Equal("文件覆写的指令", Assert.IsType<LlmNodeType>(implement.Type).Parameters?.Prompt);
    }

    [Fact]
    public void Port_binding_value_resolves_outer_member_name()
    {
        using TestEnvironment environment = TestEnvironment.Create(MemberNameBindingFlow);

        FlowDefinition flow = environment.Flows.Find(new FlowName("默认")).ThrowIfError();

        NodeSpec deliver = flow.RootNode.Nodes![1];
        NodeSpec implement = deliver.Nodes![1].Nodes![0];
        Assert.Equal(new NodeLabel("实施"), implement.Label);
        Assert.Equal("目标条目@结论", Assert.Single(Assert.Single(implement.From).Links).Ref.Display);
    }

    [Fact]
    public void Port_binding_value_passes_through_outer_scope()
    {
        using TestEnvironment environment = TestEnvironment.Create(OuterPortBindingFlow);

        FlowDefinition flow = environment.Flows.Find(new FlowName("透传")).ThrowIfError();

        NodeSpec deliver = flow.RootNode.Nodes![1];
        NodeSpec implement = deliver.Nodes![0].Nodes![0];
        Assert.Equal("制定计划@计划", Assert.Single(Assert.Single(implement.From).Links).Ref.Display);
    }

    /// <summary>绑定值带端口选取时端口随绑定展开保留，不退回整份引用。</summary>
    [Fact]
    public void Port_binding_value_keeps_port_selection()
    {
        using TestEnvironment environment = TestEnvironment.Create(PortSelectionBindingFlow);

        FlowDefinition flow = environment.Flows.Find(new FlowName("默认")).ThrowIfError();

        NodeSpec deliver = flow.RootNode.Nodes![1];
        NodeSpec implement = deliver.Nodes![0];
        Assert.Equal(new PortName("结论"), Assert.Single(Assert.Single(implement.From).Links).Ref.Port);
        Assert.Equal(new NodeLabel("来源"), Assert.Single(Assert.Single(implement.From).Links).Ref.Source);
        Assert.Equal("来源@结论", Assert.Single(Assert.Single(implement.From).Links).Ref.Display);
    }

    [Fact]
    public void Reference_executable_inherits_library_validate_and_injects_from()
    {
        using TestEnvironment environment = TestEnvironment.Create(ReferenceInheritFlow);

        FlowDefinition flow = environment.Flows.Find(new FlowName("默认")).ThrowIfError();
        NodeSpec reviewed = flow.RootNode.Nodes![1];
        Assert.Equal(ValidationPredicate.TextContains, reviewed.Spec!.Validate!.Predicate);
        Assert.Equal("计划@上下文", Assert.Single(Assert.Single(reviewed.From).Links).Ref.Display);
    }

    [Fact]
    public void Reference_validate_overrides_library_definition()
    {
        using TestEnvironment environment = TestEnvironment.Create(ReferenceOverrideValidateFlow);

        FlowDefinition flow = environment.Flows.Find(new FlowName("默认")).ThrowIfError();
        NodeSpec reviewed = flow.RootNode.Nodes![1];
        Assert.Equal(ValidationPredicate.NonEmpty, reviewed.Spec!.Validate!.Predicate);
    }

    [Fact]
    public void Reference_executable_with_inherited_validate_blocks_bad_output()
    {
        using TestEnvironment environment = TestEnvironment.Create(ReferenceInheritFlow);
        environment.Runner.PortValuesJson = """{ "结论": "修订产出不达标" }""";

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot blocked = environment.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(NodeState.Blocked, Assert.Single(blocked.ExecutableStates, state => state.Node.Label == new NodeLabel("审查实例")).State);
    }

    [Fact]
    public void Reference_executable_with_injected_group_releases_done()
    {
        using TestEnvironment environment = TestEnvironment.Create(ReferenceInheritFlow);
        environment.Runner.PortValuesJson = """{ "结论": "修订产出已通过" }""";

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables[1].Executions);
    }

    [Theory]
    [InlineData(LibraryDuplicateName, "节点名重复")]
    [InlineData(LibraryReferenceAtRoot, "节点库定义不能是引用")]
    [InlineData(LibraryLeafFrom, "执行节点库定义的接线由引用处提供")]
    [InlineData(UnknownLibraryReference, "不在节点库")]
    [InlineData(UnboundPort, "没有绑定来源")]
    [InlineData(ReferenceWithBody, "引用成员不能同时声明子节点")]
    [InlineData(ReferenceWithQuestion, "引用节点不能声明类型参数 Question")]
    [InlineData(ContainerWithQuestion, "字段 Question 不属于 Container 节点")]
    [InlineData(UnknownPortBinding, "不在容器")]
    [InlineData(PortNotDeclared, "没有在容器")]
    [InlineData(ReferenceContainerFrom, "引用容器不能声明 From")]

    [InlineData(LibraryLeafIn, "执行节点不写输入端口绑定")]
    [InlineData(ContainerWithFrom, "不支持 From")]
    [InlineData(ReferenceWithInputs, "引用节点不能声明输入端口")]
    [InlineData(LeafWithInputs, "必须至少给出一组 From")]

    [InlineData(GroupReferenceWithFrom, "引用容器不能声明 From")]
    [InlineData(GroupReferenceWithValidate, "引用节点组不能声明 Validate")]
    [InlineData(MemberFromOutOfScope, "From 引用的节点")]
    [InlineData(MemberUsesStart, "不在容器 交付 的作用域里")]
    [InlineData(MemberGroupOutsideScope, "From 引用的节点")]
    [InlineData(MemberGroupUndeclaredPort, "没有在容器")]

    [InlineData(LibraryPortOnInput, "最多一个输出端口")]
    [InlineData(LibraryQuestionOnText, "字段 Question 不属于 Llm 节点")]
    [InlineData(LibraryContainerWithOutputs, "输出端口 结论 必须写 Value 或 Ref")]
    [InlineData(MemberReferenceWithOutputs, "引用节点不能声明输出端口")]
    [InlineData(LibraryContainerOutMissingMember, "不在容器 交付 的子树里")]
    [InlineData(LibraryContainerOutBadPort, "没有输出端口 不存在")]
    [InlineData(LibraryContainerOutSubContainer, "没有输出端口 结论")]
    [InlineData(LibraryContainerOutInputContext, "类型必须与绑定目标")]
    [InlineData(LeafWithOut, "缺少 @")]
    [InlineData(ReferenceWithOut, "引用节点不能声明输出端口")]
    [InlineData(AssemblyContainerOut, "没有输出端口")]
    [InlineData(LibraryNameWithAt, "节点名不能含 @")]
    [InlineData(MemberNameWithAt, "成员标签 甲@乙 不能含 @")]
    [InlineData(LibraryInputPortWithAt, "端口名不能为空或含 @")]

    [InlineData(LibraryContainerWithInputDefault, "容器输入端口 计划 不支持默认值")]
    [InlineData(LibraryContainerWithDuplicateInputs, "输入端口 计划 重复")]
    public void Invalid_library_fails_loading(string flowsJson, string expected)
    {
        ErrorOr<TestEnvironment> environment = TestEnvironment.TryCreate(flowsJson);

        Assert.True(environment.IsError);
        Assert.Contains(environment.ErrorsOrEmptyList, error => error.Description.Contains(expected));
    }

    /// <summary>流程名取库作用域的固定文本，用于验证错误按流程作用域给出。</summary>
    private const string FlowNamedLikeLibraryScope = """
        {
          "Flows": [
            {
              "Name": "节点库",
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "计划" }],
                  "Nodes": [ { "Name": "干活", "Kind": "Llm" } ]
                }
              ]
            }
          ]
        }
        """;

    private const string LibraryFlow = """
        {
          "Nodes": [
            { "Name": "执行", "Kind": "Llm" },
            {
              "Name": "交付",
              "Kind": "Container",
              "Inputs": [{ "Name": "计划" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
              "Nodes": [
                { "Label": "实施", "Use": "执行", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "计划": "@计划" } ] }
              ]
            }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                { "Name": "制定计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                { "Label": "交付", "Use": "交付", "In": { "计划": "制定计划@计划", "MODEL_NAME": "@MODEL_NAME" } }
                  ]
                }
              ]
            }
          ]
        }
        """;
    /// <summary>库容器定义的输入端口声明默认值，绑定接口没有默认值语义。</summary>
    private const string LibraryContainerWithInputDefault = """
        {
          "Nodes": [
            { "Name": "执行", "Kind": "Llm" },
            {
              "Name": "交付",
              "Kind": "Container",
              "Inputs": [{ "Name": "计划", "Default": "固定计划" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
              "Nodes": [
                { "Label": "实施", "Use": "执行", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "计划": "@计划" } ] }
              ]
            }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    { "Name": "制定计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                    { "Label": "交付", "Use": "交付", "In": { "计划": "制定计划@计划", "MODEL_NAME": "@MODEL_NAME" } }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>库容器定义的输入端口名重复，端口名在同一节点内唯一。</summary>
    private const string LibraryContainerWithDuplicateInputs = """
        {
          "Nodes": [
            { "Name": "执行", "Kind": "Llm" },
            {
              "Name": "交付",
              "Kind": "Container",
              "Inputs": [{ "Name": "计划" }, { "Name": "计划" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
              "Nodes": [
                { "Label": "实施", "Use": "执行", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "计划": "@计划" } ] }
              ]
            }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    { "Name": "制定计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                    { "Label": "交付", "Use": "交付", "In": { "计划": "制定计划@计划", "MODEL_NAME": "@MODEL_NAME" } }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>库容器内成员的上游接线条目引用环外节点，装配校验拒绝。</summary>
    private const string MemberFromOutOfScope = """
        {
          "Nodes": [
            { "Name": "干活库", "Kind": "Llm", "Inputs": [{ "Name": "结论", "Type": "Signal" }] },
            {
              "Name": "交付",
              "Kind": "Container",
              "Inputs": [{ "Name": "计划" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
              "Nodes": [
                { "Label": "实施", "Use": "干活库", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "结论": "外来@结论" } ] }
              ]
            }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Label": "交付组", "Use": "交付", "In": { "计划": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" } }
                  ]
                }
              ]
            }
          ]
        }
        """;
    /// <summary>库容器成员直接引用开始端口，容器外的来源不可见。</summary>
    private const string MemberUsesStart = """
        {
          "Nodes": [
            { "Name": "执行", "Kind": "Llm" },
            { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "Nodes": [ { "Label": "实施", "Use": "执行", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "Start@目标" } ] } ] }
          ],
          "Flows": [
            { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [ { "Label": "交付", "Use": "交付", "In": { "MODEL_NAME": "Start@MODEL_NAME" } } ] }
          ]
        }
        """;

    private const string DualInstanceFlow = """
        {
          "Nodes": [
            { "Name": "规划", "Kind": "Llm", "Outputs": [{ "Name": "计划", "Type": "Text" }] },
            { "Name": "执行", "Kind": "Llm" },
            { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "计划" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "Nodes": [
              { "Label": "实施", "Use": "执行", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "计划": "@计划" } ] }
            ] }
          ],
          "Flows": [
            {
              "Name": "双线",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                { "Label": "制定A", "Use": "规划", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] },
                { "Label": "制定B", "Use": "规划", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] },
                { "Label": "交付A", "Use": "交付", "In": { "计划": "制定A@计划", "MODEL_NAME": "@MODEL_NAME" } },
                { "Label": "交付B", "Use": "交付", "In": { "计划": "制定B@计划", "MODEL_NAME": "@MODEL_NAME" } }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private const string PerFlowModelBinding = """
        {
          "Nodes": [
            { "Name": "执行", "Kind": "Llm" },
            { "Name": "交付", "Kind": "Container",
              "Inputs": [{ "Name": "计划" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
              "Nodes": [
                { "Label": "实施", "Use": "执行", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "计划": "@计划" } ] }
              ] }
          ],
          "Flows": [
            {
              "Name": "甲",
              "Start": { "Outputs": [
                { "Name": "目标", "Type": "Text" },
                { "Name": "MODEL_NAME", "Type": "Text", "Value": "执行者", "Static": true }
              ] },
              "Nodes": [ { "Name": "整体", "Kind": "Container",
                "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
                "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" },
                "Nodes": [
                  { "Name": "制定计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                  { "Label": "交付", "Use": "交付", "In": { "计划": "制定计划@计划", "MODEL_NAME": "@MODEL_NAME" } }
                ] } ]
            },
            {
              "Name": "乙",
              "Start": { "Outputs": [
                { "Name": "目标", "Type": "Text" },
                { "Name": "MODEL_NAME", "Type": "Text", "Value": "实施者", "Static": true }
              ] },
              "Nodes": [ { "Name": "整体", "Kind": "Container",
                "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
                "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" },
                "Nodes": [
                  { "Name": "制定计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                  { "Label": "交付", "Use": "交付", "In": { "计划": "制定计划@计划", "MODEL_NAME": "@MODEL_NAME" } }
                ] } ]
            }
          ]
        }
        """;

    private const string LibraryDuplicateName = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm" },
          { "Name": "执行", "Kind": "Llm" }
        ], "Flows": [] }
        """;

    private const string LibraryReferenceAtRoot = """
        { "Nodes": [ { "Name": "执行", "Kind": "Llm" }, { "Label": "副本", "Use": "执行" } ], "Flows": [] }
        """;

    private const string LibraryLeafFrom = """
        { "Nodes": [ { "Name": "执行", "Kind": "Llm", "From": [ { "计划": "制定计划@计划" } ] } ], "Flows": [] }
        """;

    private const string UnknownLibraryReference = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm" },
          { "Name": "交付", "Kind": "Container", "Nodes": [ { "Label": "实施", "Use": "不存在", "Static": { "MODEL_NAME": "@MODEL_NAME" } } ] }
        ], "Flows": [
          { "Name": "默认", "Nodes": [ { "Label": "交付", "Use": "交付" } ] }
        ] }
        """;

    private const string UnknownReferenceWithoutStatic = """
        { "Nodes": [ { "Name": "执行", "Kind": "Llm" } ], "Flows": [
          { "Name": "默认", "Nodes": [ { "Name": "交付", "Kind": "Container", "Nodes": [ { "Label": "实施", "Use": "不存在" } ] } ] }
        ] }
        """;

    private const string UnboundPort = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm" },
          { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "计划" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "Nodes": [ { "Label": "实施", "Use": "执行", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "计划": "@计划" } ] } ] }
        ], "Flows": [
          { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [ { "Label": "交付", "Use": "交付", "In": { "MODEL_NAME": "Start@MODEL_NAME" } } ] }
        ] }
        """;

    private const string PortNotDeclared = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm" },
          { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "计划" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "Nodes": [ { "Label": "实施", "Use": "执行", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "来源": "@来源" } ] } ] }
        ], "Flows": [
          { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [ { "Label": "交付", "Use": "交付", "In": { "MODEL_NAME": "Start@MODEL_NAME" } } ] }
        ] }
        """;

    private const string ReferenceWithBody = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm" },
          { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "计划" }], "Nodes": [ { "Label": "实施", "Use": "执行", "From": [ { "计划": "@计划" } ], "Nodes": [ { "Name": "内", "Kind": "Container" } ] } ] }
        ], "Flows": [
          { "Name": "默认", "Nodes": [ { "Label": "交付", "Use": "交付", "In": { "计划": "制定计划@计划" } } ] }
        ] }
        """;

    private const string UnknownPortBinding = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm" },
          { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "计划" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "Nodes": [ { "Label": "实施", "Use": "执行", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "计划": "@计划" } ] } ] }
        ], "Flows": [
          { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [ { "Label": "交付", "Use": "交付", "In": { "来源": "制定计划@计划", "MODEL_NAME": "Start@MODEL_NAME" } } ] }
        ] }
        """;

    private const string LibraryLeafIn = """
        { "Nodes": [ { "Name": "执行", "Kind": "Llm", "In": { "x": "y" } } ], "Flows": [] }
        """;

    private const string ContainerWithFrom = """
        { "Nodes": [ { "Name": "组", "Kind": "Container", "Inputs": [{ "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "From": [ { "x": "x" } ], "Nodes": [ { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" } } ] } ], "Flows": [] }
        """;

    private const string ReferenceWithInputs = """
        { "Nodes": [ { "Name": "执行", "Kind": "Llm" } ], "Flows": [
          { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [ { "Name": "整体", "Kind": "Container", "Nodes": [ { "Label": "实施", "Use": "执行", "Inputs": [{ "Name": "计划" }] } ] } ] }
        ] }
        """;

    private const string LeafWithInputs = """
        { "Nodes": [], "Flows": [
          { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [ { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [ { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "计划" }] } ] } ] }
        ] }
        """;

    private const string ReferenceWithQuestion = """
        { "Nodes": [ { "Name": "执行", "Kind": "Llm" } ], "Flows": [
          { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [ { "Name": "整体", "Kind": "Container", "Nodes": [ { "Label": "实施", "Use": "执行", "Question": "确认" } ] } ] }
        ] }
        """;

    private const string ContainerWithQuestion = """
        { "Nodes": [], "Flows": [
          { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [ { "Name": "整体", "Kind": "Container", "Question": "确认", "Nodes": [ { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" } } ] } ] }
        ] }
        """;

    private const string ReferenceContainerFrom = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm" },
          { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "计划" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "Nodes": [ { "Label": "实施", "Use": "执行", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "计划": "@计划" } ] } ] }
        ], "Flows": [
          { "Name": "默认", "Nodes": [ { "Label": "交付", "Use": "交付", "From": [ { "计划": "制定计划@计划" } ] } ] }
        ] }
        """;

    private const string OuterPortBindingFlow = """
        {
          "Nodes": [
            { "Name": "执行", "Kind": "Llm" },
            { "Name": "内层", "Kind": "Container", "Inputs": [{ "Name": "内部" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "Nodes": [
              { "Label": "实施", "Use": "执行", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "内部": "@内部" } ] }
            ] },
            { "Name": "外层", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "Nodes": [
              { "Label": "里", "Use": "内层", "In": { "内部": "@目标", "MODEL_NAME": "@MODEL_NAME" } }
            ] }
          ],
          "Flows": [
            {
              "Name": "透传",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                { "Name": "制定计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Prompt": "把目标写成可实施的计划。", "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                { "Label": "交付", "Use": "外层", "In": { "目标": "制定计划@计划", "MODEL_NAME": "@MODEL_NAME" } }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>绑定值带端口选取：容器 In 绑定 来源@端口，成员 @端口 展开后保留端口选取。</summary>
    private const string PortSelectionBindingFlow = """
        {
          "Nodes": [
            { "Name": "分析", "Kind": "Llm", "Outputs": [{ "Name": "结论", "Type": "Text" }] },
            { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "输入" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "Nodes": [
              { "Label": "实施", "Use": "分析", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "输入": "@输入" } ] }
            ] }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    { "Label": "来源", "Use": "分析", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] },
                    { "Label": "交付", "Use": "交付", "In": { "输入": "来源@结论", "MODEL_NAME": "@MODEL_NAME" } }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private const string DuplicateImportedFlows = """
        { "Flows": [
          { "Name": "重复", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [ { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "Start@MODEL_NAME" }, "From": [ { "目标": "Start@目标" } ] } ] },
          { "Name": "重复", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [ { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "Start@MODEL_NAME" }, "From": [ { "目标": "Start@目标" } ] } ] }
        ] }
        """;

    /// <summary>只写流程，引用的节点库定义不在文件里，按文件自身装配会报缺定义。</summary>
    private const string LibraryReferenceOnlyFlow = """
        { "Flows": [
          { "Name": "复用", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Label": "干活", "Use": "外部库", "Static": { "MODEL_NAME": "@MODEL_NAME" } }
            ] }
          ] }
        ] }
        """;

    /// <summary>文件顶层定义与内置「执行」同名的节点，覆盖内置定义。</summary>
    private const string OverridingBuiltinFlow = """
        {
          "Nodes": [
            { "Name": "执行", "Kind": "Llm", "Prompt": "文件覆写的指令" }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [ { "Label": "实施", "Use": "执行", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] } ] }
              ]
            }
          ]
        }
        """;

    /// <summary>文件只定义自己的节点，流程同时引用内置「交付」容器。</summary>
    private const string BuiltinReferenceFlow = """
        {
          "Nodes": [
            { "Name": "准备", "Kind": "Llm", "Prompt": "写出计划", "Outputs": [{ "Name": "计划", "Type": "Text" }] }
          ],
          "Flows": [
            {
              "Name": "内置引用",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    { "Label": "定计划", "Use": "准备", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] },
                    { "Label": "交付", "Use": "交付", "In": { "计划": "定计划@计划", "MODEL_NAME": "@MODEL_NAME" } }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>一条新名字的流程，可独立装配。</summary>
    private const string ExtraFlow = """
        { "Flows": [
          { "Name": "额外", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "干活", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] }
            ] }
          ] }
        ] }
        """;

    /// <summary>一条与测试自带流程同名的流程，可独立装配。</summary>
    private const string OverwritingFlow = """
        { "Flows": [
          { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "干活", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] }
            ] }
          ] }
        ] }
        """;

    private const string MemberNameBindingFlow = """
        {
          "Nodes": [
            { "Name": "执行", "Kind": "Llm" },
            { "Name": "内层", "Kind": "Container", "Inputs": [{ "Name": "内部" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "Nodes": [
              { "Label": "实施", "Use": "执行", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "内部": "@内部" } ] }
            ] },
            { "Name": "外层", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "入口" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "Nodes": [
              { "Name": "目标条目", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "入口": "@入口" } ] },
              { "Label": "里", "Use": "内层", "In": { "内部": "目标条目@结论", "MODEL_NAME": "@MODEL_NAME" } }
            ] }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                { "Name": "规划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                { "Label": "交付", "Use": "外层", "In": { "目标": "规划@计划", "入口": "@目标", "MODEL_NAME": "@MODEL_NAME" } }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>文档示例的形状可加载：库定义、执行节点定义、容器端口绑定。</summary>
    [Fact]
    public void Document_example_loads_with_library_and_reference()
    {
        using TestEnvironment environment = TestEnvironment.Create(DocumentExampleFlow);

        FlowDefinition flow = environment.Flows.Find(new FlowName("单线交付")).ThrowIfError();
        NodeSpec root = flow.RootNode;
        Assert.NotNull(root.Nodes);
        Assert.Equal(2, root.Nodes.Count);
    }

    private const string DocumentExampleFlow = """
        {
          "Nodes": [
            {
              "Name": "允许工具",
              "Kind": "Llm",
              "Tools": ["GetLocalTime"]
            },
            {
              "Name": "交付",
              "Kind": "Container",
              "Inputs": [{ "Name": "计划" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
              "Nodes": [
                { "Label": "实施", "Use": "允许工具", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "计划": "@计划" } ] }
              ]
            }
          ],
          "Flows": [
            {
              "Name": "单线交付",
              "Description": "制定计划、复用交付",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                { "Name": "制定计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Prompt": "把目标写成可实施的计划。", "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                { "Label": "交付", "Use": "交付", "In": { "计划": "制定计划@计划", "MODEL_NAME": "@MODEL_NAME" } }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>库执行定义带校验，引用处注入启动组与数据接线。</summary>
    private const string ReferenceInheritFlow = """
        {
          "Nodes": [
            {
              "Name": "审查",
              "Kind": "Llm",
              "Outputs": [{ "Name": "结论", "Type": "Text" }],
              "Validate": { "Predicate": "TextContains", "Argument": "通过" }
            }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    { "Name": "计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "上下文", "Type": "Context" }], "From": [ { "目标": "@目标" } ] },
                    { "Label": "审查实例", "Use": "审查", "Static": { "MODEL_NAME": "@MODEL_NAME" },
                      "From": [ { "上下文": "计划@上下文" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>引用侧显式声明校验时覆盖库定义。</summary>
    private const string ReferenceOverrideValidateFlow = """"
        {
          "Nodes": [
            {
              "Name": "审查",
              "Kind": "Llm",
              "Outputs": [{ "Name": "结论", "Type": "Text" }],
              "Validate": { "Predicate": "TextContains", "Argument": "通过" }
            }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    { "Name": "计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] },
                    { "Label": "审查实例", "Use": "审查", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ],
                      "Validate": { "Predicate": "NonEmpty" } }
                  ]
                }
              ]
            }
          ]
        }
        """";

    /// <summary>引用节点组不能声明 From，接线只属于执行节点。</summary>
    private const string GroupReferenceWithFrom = """"
        {
          "Nodes": [
            { "Name": "审查", "Kind": "Llm" },
            {
              "Name": "组",
              "Kind": "Container",
              "Inputs": [{ "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
              "Nodes": [ { "Label": "甲", "Use": "审查", "Static": { "MODEL_NAME": "@MODEL_NAME" } } ]
            }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" } },
                    { "Label": "引用组", "Use": "组", "From": [ { "结论": "计划@结论" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """";
    /// <summary>引用节点组不能带输出校验，校验只属于执行节点。</summary>
    private const string GroupReferenceWithValidate = """"
        {
          "Nodes": [
            { "Name": "审查", "Kind": "Llm" },
            {
              "Name": "组",
              "Kind": "Container",
              "Inputs": [{ "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
              "Nodes": [ { "Label": "甲", "Use": "审查", "Static": { "MODEL_NAME": "@MODEL_NAME" } } ]
            }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" } },
                    { "Label": "引用组", "Use": "组", "Validate": { "Predicate": "NonEmpty" } }
                  ]
                }
              ]
            }
          ]
        }
        """";

    /// <summary>库容器成员执行节点的启动组来源必须落在容器作用域内。</summary>
    private const string MemberGroupOutsideScope = """"
        {
          "Nodes": [
            {
              "Name": "交付",
              "Kind": "Container",
              "Inputs": [{ "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
              "Nodes": [
                { "Name": "甲", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "结论": "局外@结论" } ] }
              ]
            }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Label": "交付实例", "Use": "交付" }
                  ]
                }
              ]
            }
          ]
        }
        """";

    /// <summary>库容器成员执行节点的启动组端口必须已由容器定义声明。</summary>
    private const string MemberGroupUndeclaredPort = """"
        {
          "Nodes": [
            {
              "Name": "交付",
              "Kind": "Container",
              "Inputs": [{ "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
              "Nodes": [
                { "Name": "甲", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "计划": "@计划" } ] }
              ]
            }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Label": "交付实例", "Use": "交付" }
                  ]
                }
              ]
            }
          ]
        }
        """";

    /// <summary>输入成员在节点库声明输出端口，装配校验拒绝。</summary>
    private const string LibraryPortOnInput = """
        {
          "Nodes": [
            { "Name": "收集", "Kind": "Input", "Outputs": [{ "Name": "结论", "Type": "Text" }, { "Name": "理由", "Type": "Text" }] }
          ]
        }
        """;

    /// <summary>库执行定义声明 Question，装配校验拒绝。</summary>
    private const string LibraryQuestionOnText = """
        { "Nodes": [ { "Name": "干活库", "Kind": "Llm", "Question": "你想问什么" } ], "Flows": [] }
        """;

    /// <summary>库容器定义声明输出端口，装配校验拒绝。</summary>
    private const string LibraryContainerWithOutputs = """
        { "Nodes": [
          { "Name": "交付", "Kind": "Container", "Outputs": [{ "Name": "结论", "Type": "Text" }], "Nodes": [ { "Name": "实施", "Kind": "Llm" } ] }
        ], "Flows": [] }
        """;

    /// <summary>容器子树里的引用成员声明输出端口，装配校验拒绝。</summary>
    private const string MemberReferenceWithOutputs = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm" },
          { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "Nodes": [
            { "Label": "实施", "Use": "执行", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }] }
          ] }
        ], "Flows": [] }
        """;

    /// <summary>库定义节点标签含 @，与端口引用分隔符冲突，装配校验拒绝。</summary>
    private const string LibraryNameWithAt = """
        { "Nodes": [ { "Name": "甲@乙", "Kind": "Llm" } ], "Flows": [] }
        """;

    /// <summary>库容器成员标签含 @，与端口引用分隔符冲突，装配校验拒绝。</summary>
    private const string MemberNameWithAt = """
        { "Nodes": [
          { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "Nodes": [ { "Name": "甲@乙", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" } } ] }
        ], "Flows": [] }
        """;

    /// <summary>库容器输入端口名含 @，装配校验拒绝。</summary>
    private const string LibraryInputPortWithAt = """
        { "Nodes": [
          { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "计划@甲" }], "Nodes": [ { "Name": "实施", "Kind": "Llm" } ] }
        ], "Flows": [] }
        """;

    /// <summary>库容器输出端口绑定目标不在容器子树内，装配校验拒绝。</summary>
    private const string LibraryContainerOutMissingMember = """
        { "Nodes": [
          { "Name": "交付", "Kind": "Container", "Outputs": [{ "Name": "结论", "Ref": "外部@结论" }], "Nodes": [ { "Name": "实施", "Kind": "Llm" } ] }
        ], "Flows": [] }
        """;

    /// <summary>库容器输出端口绑定目标端口未声明，装配校验拒绝。</summary>
    private const string LibraryContainerOutBadPort = """
        { "Nodes": [
          {
            "Name": "交付",
            "Kind": "Container",
            "Outputs": [{ "Name": "结论", "Ref": "实施@不存在" }],
            "Nodes": [ { "Name": "实施", "Kind": "Llm", "Outputs": [{ "Name": "结论", "Type": "Text" }] } ]
          }
        ], "Flows": [] }
        """;

    /// <summary>库容器输出端口绑定子树里的输入节点声明的上下文端口，输入节点不落执行，不能作为上下文端口来源。</summary>
    private const string LibraryContainerOutInputContext = """
        { "Nodes": [
          { "Name": "交付", "Kind": "Container", "Outputs": [{ "Name": "结果", "Ref": "收集@背景" }], "Nodes": [ { "Name": "收集", "Kind": "Input", "Outputs": [{ "Name": "背景", "Type": "Context" }] } ] }
        ], "Flows": [] }
        """;

    /// <summary>库容器输出端口绑定子树里的子容器，子容器不是执行节点，装配校验拒绝。</summary>
    private const string LibraryContainerOutSubContainer = """
        { "Nodes": [
            {
              "Name": "交付",
              "Kind": "Container",
              "Outputs": [{ "Name": "结果", "Ref": "撰写组@结论" }],
              "Nodes": [
                { "Name": "撰写组", "Kind": "Container", "Nodes": [ { "Name": "实施", "Kind": "Llm", "Outputs": [{ "Name": "结论", "Type": "Text" }] } ] }
              ]
            }
          ], "Flows": [] }
        """;

    /// <summary>执行节点声明容器输出端口，装配校验拒绝。</summary>
    private const string LeafWithOut = """
        { "Nodes": [
          { "Name": "交付", "Kind": "Container", "Nodes": [ { "Name": "实施", "Kind": "Llm", "Outputs": [{ "Name": "结论", "Ref": "实施" }] } ] }
        ], "Flows": [] }
        """;

    /// <summary>引用节点声明容器输出端口，装配校验拒绝。</summary>
    private const string ReferenceWithOut = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm" },
          { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "Nodes": [ { "Label": "实施", "Use": "执行", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Ref": "实施" }] } ] }
        ], "Flows": [] }
        """;

    /// <summary>装配层内联容器声明输出端口，绑定目标端口必须被成员声明。</summary>
    private const string AssemblyContainerOut = """
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Outputs": [{ "Name": "结论", "Ref": "实施@不存在" }], "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [ { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] } ] }
        ] } ] }
        """;

    /// <summary>校验节点库，取错误说明。</summary>
    private static IReadOnlyList<string> LibraryErrors(params NodeSpec[] library) =>
        [.. new NodeLibraryExpander().Expand([], [], library).ErrorsOrEmptyList.Select(error => error.Description)];
}

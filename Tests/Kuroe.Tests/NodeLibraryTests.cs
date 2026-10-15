using System.Text.Json;
using ErrorOr;
using Kuroe.Configuration;
using Kuroe.Configuration.Files;
using Kuroe.Flow.NodeDefinition;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.StaticGraph;
using Kuroe.Flow.NodeType.Llm;
using Kuroe.Workflows.Flows.Files;
using Xunit;

namespace Kuroe.Tests;

/// <summary>节点库与装配引用：定义复用、端口绑定、实例命名与保存往返。</summary>
public sealed class NodeLibraryTests
{
    [Fact]
    public void Flow_references_library_leaf_and_container_with_port_binding()
    {
        using KuroeHarness harness = KuroeHarness.Create(LibraryFlow);

        FlowDefinition flow = harness.Flows.Find(new FlowName("默认")).ThrowIfError();

        // 展开后：根容器包住制定计划与交付容器，容器成员名带实例前缀，@计划 绑定为制定计划
        NodeSpec root = flow.RootNode;
        Assert.NotNull(root.Nodes);
        Assert.Equal(2, root.Nodes.Count);
        NodeSpec plan = root.Nodes[0];
        Assert.NotNull(plan.Execution);
        Assert.Equal(BuiltinNodeKinds.Llm, plan.Kind);
        Assert.Equal("Start@目标", Assert.Single(Assert.Single(plan.From).Links).Ref.Display);

        NodeSpec deliver = root.Nodes[1];
        Assert.NotNull(deliver.Nodes);
        NodeSpec implement = deliver.Nodes[0];
        Assert.Equal(new NodeName("交付.实施"), implement.Name);
        Assert.NotNull(implement.Execution);
        Assert.Equal(NodeMode.PerItem, implement.Execution.Mode);
        Assert.Equal("制定计划@拆分", Assert.Single(Assert.Single(implement.From).Links).Ref.Display);
    }

    [Fact]
    public void Same_library_container_instantiated_twice_keeps_members_distinct()
    {
        using KuroeHarness harness = KuroeHarness.Create(DualInstanceFlow);

        TaskSnapshot done = harness.Settle(harness.Submit("补齐 README", new FlowName("双线")));

        Assert.Equal(TaskState.Done, done.State);
    }

    [Fact]
    public void Container_model_slot_bound_to_distinct_models_per_flow()
    {
        using KuroeHarness harness = KuroeHarness.Create(PerFlowModelBinding);

        FlowDefinition first = harness.Flows.Find(new FlowName("甲")).ThrowIfError();
        NodeSpec firstImplement = first.RootNode.Nodes![1].Nodes![0];
        Assert.Equal(new ModelRef("执行者"), firstImplement.Model!.Value);

        FlowDefinition second = harness.Flows.Find(new FlowName("乙")).ThrowIfError();
        NodeSpec secondImplement = second.RootNode.Nodes![1].Nodes![0];
        Assert.Equal(new ModelRef("实施者"), secondImplement.Model!.Value);
    }

    [Fact]
    public void Unknown_reference_does_not_report_model_slot_loss()
    {
        ErrorOr<KuroeHarness> harness = KuroeHarness.TryCreate(UnknownReferenceWithoutModel);

        Assert.True(harness.IsError);
        Assert.Contains(harness.ErrorsOrEmptyList, error => error.Description.Contains("不在节点库"));
        Assert.DoesNotContain(harness.ErrorsOrEmptyList, error => error.Description.Contains("必须声明模型槽位"));
    }

    [Fact]
    public void Import_keeps_library_and_reference_shape_on_save()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        File.WriteAllText(Path.Combine(harness.Root, "library.json"), LibraryFlow);

        FlowImport imported = harness.Flows.Import("library.json").ThrowIfError();
        Assert.Contains(imported.Notes, note => note.Contains("节点库"));

        string text = File.ReadAllText(KuroePaths.At(harness.Root).FlowsFile);
        Assert.Contains("\"Nodes\"", text);
        Assert.Contains("\"Use\"", text);
        Assert.Contains("\"In\"", text);
        Assert.Contains("\"Inputs\"", text);
        Assert.Contains("\"Model\"", text);
        Assert.Contains("\"Models\"", text);

        // 保存后容器定义写槽位声明数组，引用处写槽位绑定对象
        using JsonDocument saved = JsonDocument.Parse(text);
        JsonElement libraryContainer = saved.RootElement.GetProperty("Nodes")
            .EnumerateArray()
            .Single(node => node.GetProperty("Name").GetString() == "交付");
        Assert.Equal(JsonValueKind.Array, libraryContainer.GetProperty("Models").ValueKind);

        JsonElement reference = saved.RootElement.GetProperty("Flows")[0].GetProperty("Nodes")[0].GetProperty("Nodes")[1];
        Assert.Equal(JsonValueKind.Object, reference.GetProperty("Models").ValueKind);
    }

    /// <summary>文件定义自定义节点时，流程仍可直接引用内置节点；落盘只写文件来源的节点定义。</summary>
    [Fact]
    public void Builtin_node_stays_usable_and_unwritten_when_file_defines_its_own()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        File.WriteAllText(Path.Combine(harness.Root, "builtin.json"), BuiltinReferenceFlow);

        harness.Flows.Import("builtin.json").ThrowIfError();

        NodeSpec container = harness.Flows.Find(new FlowName("内置引用")).ThrowIfError().RootNode.Nodes![1];
        Assert.NotNull(container.Nodes);
        NodeSpec implement = container.Nodes[0];
        Assert.NotNull(implement.Execution);
        Assert.Equal(new NodeName("交付.实施"), implement.Name);
        Assert.Equal(NodeMode.PerItem, implement.Execution.Mode);

        var (Nodes, _) = FlowReader.ToContent(FlowStore.ReadDto(KuroePaths.At(harness.Root).FlowsFile).ThrowIfError(), TestNodeTypes.Registry()).ThrowIfError();
        IReadOnlyList<NodeName> savedNodes = [.. Nodes.Select(node => node.Name)];
        Assert.Equal([new NodeName("准备")], savedNodes);
    }

    /// <summary>库容器内成员的上游接线条目必须落在子树或声明端口内，环外成员不可见。</summary>
    [Fact]
    public void Member_from_outside_container_scope_is_rejected()
    {
        ErrorOr<KuroeHarness> harness = KuroeHarness.TryCreate(MemberFromOutOfScope);

        Assert.True(harness.IsError);
        Assert.Contains(harness.ErrorsOrEmptyList,
            error => error.Description.Contains("From 引用的节点 外来 不在容器 实施 的作用域里"));
    }

    [Fact]
    public void Import_rejects_duplicated_flow_names()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        File.WriteAllText(Path.Combine(harness.Root, "duplicated.json"), DuplicateImportedFlows);

        ErrorOr<FlowImport> imported = harness.Flows.Import("duplicated.json");

        Assert.True(imported.IsError);
        Assert.Contains(imported.ErrorsOrEmptyList, error => error.Description.Contains("流程名重复"));
    }

    /// <summary>库执行定义的次数上限非正，未经引用也在库校验阶段拒绝。</summary>
    [Fact]
    public void Library_executable_with_non_positive_max_runs_is_rejected() =>
        Assert.Equal(
            ["节点库定义 干活库：MaxRuns 必须是正整数。"],
            LibraryErrors(new NodeSpec { Name = new NodeName("干活库"), Kind = BuiltinNodeKinds.Llm, Execution = new ExecutableSpec { MaxRuns = 0 } }));

    /// <summary>库容器定义的非正统一上限，未经引用也在库校验阶段拒绝。</summary>
    [Fact]
    public void Library_container_with_non_positive_max_runs_is_rejected() =>
        Assert.Equal(
            ["节点库定义 干活组：MaxRuns 必须是正整数。"],
            LibraryErrors(new NodeSpec
            {
                Name = new NodeName("干活组"),
                Kind = BuiltinNodeKinds.Container,
                MaxRuns = 0,
                ModelSlots = [new ModelRef("执行者")],
                Nodes = [new NodeSpec { Name = new NodeName("干活"), Kind = BuiltinNodeKinds.Llm, Model = new ModelRef("执行者"), Execution = new ExecutableSpec() }],
            }));

    /// <summary>库容器定义必须声明子节点，缺子节点在库校验阶段拒绝。</summary>
    [Fact]
    public void Library_container_without_children_is_rejected() =>
        Assert.Equal(
            ["节点库定义 空组：容器节点必须声明子节点。"],
            LibraryErrors(new NodeSpec { Name = new NodeName("空组"), Kind = BuiltinNodeKinds.Container }));

    /// <summary>库执行定义不能声明子节点，子节点只属于容器。</summary>
    [Fact]
    public void Library_executable_with_children_is_rejected() =>
        Assert.Equal(
            ["节点库定义 干活库：执行节点与输入节点不能声明子节点，子节点只属于容器。"],
            LibraryErrors(new NodeSpec
            {
                Name = new NodeName("干活库"),
                Kind = BuiltinNodeKinds.Llm,
                Execution = new ExecutableSpec(),
                Nodes = [new NodeSpec { Name = new NodeName("干活"), Kind = BuiltinNodeKinds.Llm, Execution = new ExecutableSpec() }],
            }));

    /// <summary>库容器定义不写执行节点的输出端口与输出校验，校验阶段拒绝。</summary>
    [Fact]
    public void Library_container_with_execution_fields_is_rejected() =>
        Assert.Equal(
            [
                "节点库定义 干活组：库容器定义不声明执行节点输出端口，容器输出端口条目写 Name 与 Ref。",
                "节点库定义 干活组：库容器定义不声明 Validate，输出校验只属于执行节点。",
            ],
            LibraryErrors(new NodeSpec
            {
                Name = new NodeName("干活组"),
                Kind = BuiltinNodeKinds.Container,
                Outputs = [new Port(new PortName("结论"), PortType.Text)],
                Validate = new OutputValidation(ValidationPredicate.NonEmpty, null),
                ModelSlots = [new ModelRef("执行者")],
                Nodes = [new NodeSpec { Name = new NodeName("干活"), Kind = BuiltinNodeKinds.Llm, Model = new ModelRef("执行者"), Execution = new ExecutableSpec() }],
            }));

    /// <summary>库执行定义不写容器输出端口，那属于容器定义。</summary>
    [Fact]
    public void Library_executable_with_container_out_is_rejected() =>
        Assert.Equal(
            ["节点库定义 干活库：库执行定义不声明容器输出端口，执行节点输出端口条目写 Name 与 Type。"],
            LibraryErrors(new NodeSpec
            {
                Name = new NodeName("干活库"),
                Kind = BuiltinNodeKinds.Llm,
                Execution = new ExecutableSpec(),
                ContainerOutputs = new Dictionary<PortName, ContainerOutput> { [new PortName("结论")] = new(PortRef.Named(new NodeName("干活"), new PortName("结论"))) },
            }));

    /// <summary>流程名与库作用域名相同时错误仍按流程作用域给出，装配层容器的输入端口声明仍被拒。</summary>
    [Fact]
    public void Flow_named_like_library_scope_keeps_flow_errors()
    {
        ErrorOr<KuroeHarness> created = KuroeHarness.TryCreate(FlowNamedLikeLibraryScope);

        Assert.Equal(
            ["流程 节点库 的节点 整体：容器节点不能声明输入端口，端口声明属于节点库容器定义。"],
            created.ErrorsOrEmptyList.Select(error => error.Description));
    }

    /// <summary>导入的文件要能独立装配：流程引用的节点库定义不在同一文件里时不生效。</summary>
    [Fact]
    public void Import_rejects_flow_referencing_library_node_outside_the_file()
    {
        using KuroeHarness harness = KuroeHarness.Create(LibraryFlow);
        File.WriteAllText(Path.Combine(harness.Root, "reuse.json"), LibraryReferenceOnlyFlow);

        ErrorOr<FlowImport> imported = harness.Flows.Import("reuse.json");

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
        using KuroeHarness harness = KuroeHarness.Create();
        File.WriteAllText(Path.Combine(harness.Root, "extra.json"), ExtraFlow);

        FlowImport imported = harness.Flows.Import("extra.json").ThrowIfError();

        Assert.Equal(["新增流程 额外"], imported.Notes);
    }

    /// <summary>导入与现有流程同名的流程时逐条说明覆盖。</summary>
    [Fact]
    public void Import_reports_overwritten_flow()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        File.WriteAllText(Path.Combine(harness.Root, "extra.json"), OverwritingFlow);

        FlowImport imported = harness.Flows.Import("extra.json").ThrowIfError();

        Assert.Equal(["覆盖流程 默认"], imported.Notes);
    }

    /// <summary>文件定义的节点与内置节点同名时覆盖内置定义。</summary>
    [Fact]
    public void File_node_overrides_builtin_definition()
    {
        using KuroeHarness harness = KuroeHarness.Create(OverridingBuiltinFlow);

        FlowDefinition flow = harness.Flows.Find(new FlowName("默认")).ThrowIfError();
        NodeSpec implement = flow.RootNode.Nodes![0];

        Assert.Equal("文件覆写的指令", Assert.IsType<LlmParameters>(implement.Parameters).Prompt);
    }

    [Fact]
    public void Port_binding_value_resolves_outer_member_name()
    {
        using KuroeHarness harness = KuroeHarness.Create(MemberNameBindingFlow);

        FlowDefinition flow = harness.Flows.Find(new FlowName("默认")).ThrowIfError();

        NodeSpec deliver = flow.RootNode.Nodes![1];
        NodeSpec implement = deliver.Nodes![1].Nodes![0];
        Assert.Equal(new NodeName("交付.里.实施"), implement.Name);
        Assert.Equal("交付.目标条目@结论", Assert.Single(Assert.Single(implement.From).Links).Ref.Display);
    }

    [Fact]
    public void Port_binding_value_passes_through_outer_scope()
    {
        using KuroeHarness harness = KuroeHarness.Create(OuterPortBindingFlow);

        FlowDefinition flow = harness.Flows.Find(new FlowName("透传")).ThrowIfError();

        NodeSpec deliver = flow.RootNode.Nodes![1];
        NodeSpec implement = deliver.Nodes![0].Nodes![0];
        Assert.Equal("制定计划@拆分", Assert.Single(Assert.Single(implement.From).Links).Ref.Display);
    }

    /// <summary>绑定值带端口选取时端口随绑定展开保留，不退回整份引用。</summary>
    [Fact]
    public void Port_binding_value_keeps_port_selection()
    {
        using KuroeHarness harness = KuroeHarness.Create(PortSelectionBindingFlow);

        FlowDefinition flow = harness.Flows.Find(new FlowName("默认")).ThrowIfError();

        NodeSpec deliver = flow.RootNode.Nodes![1];
        NodeSpec implement = deliver.Nodes![0];
        Assert.Equal(new PortName("结论"), Assert.Single(Assert.Single(implement.From).Links).Ref.Port);
        Assert.Equal(new NodeName("来源"), Assert.Single(Assert.Single(implement.From).Links).Ref.Source);
        Assert.Equal("来源@结论", Assert.Single(Assert.Single(implement.From).Links).Ref.Display);
    }

    [Fact]
    public void Reference_executable_inherits_library_validate_and_injects_from()
    {
        using KuroeHarness harness = KuroeHarness.Create(ReferenceInheritFlow);

        FlowDefinition flow = harness.Flows.Find(new FlowName("默认")).ThrowIfError();
        NodeSpec reviewed = flow.RootNode.Nodes![1];
        Assert.Equal(ValidationPredicate.TextContains, reviewed.Execution!.Validate!.Predicate);
        Assert.Equal("计划@上下文", Assert.Single(Assert.Single(reviewed.From).Links).Ref.Display);
    }

    [Fact]
    public void Reference_validate_overrides_library_definition()
    {
        using KuroeHarness harness = KuroeHarness.Create(ReferenceOverrideValidateFlow);

        FlowDefinition flow = harness.Flows.Find(new FlowName("默认")).ThrowIfError();
        NodeSpec reviewed = flow.RootNode.Nodes![1];
        Assert.Equal(ValidationPredicate.NonEmpty, reviewed.Execution!.Validate!.Predicate);
    }

    [Fact]
    public void Reference_executable_with_inherited_validate_blocks_bad_output()
    {
        using KuroeHarness harness = KuroeHarness.Create(ReferenceInheritFlow);
        harness.Executor.PortValuesJson = """{ "结论": "修订产出不达标" }""";

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(NodeState.Blocked, Assert.Single(blocked.ExecutableStates, state => state.Index == 3).State);
    }

    [Fact]
    public void Reference_executable_with_injected_group_releases_done()
    {
        using KuroeHarness harness = KuroeHarness.Create(ReferenceInheritFlow);
        harness.Executor.PortValuesJson = """{ "结论": "修订产出已通过" }""";

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables[1].Runs);
    }

    [Theory]
    [InlineData(LibraryDuplicateName, "节点名重复")]
    [InlineData(LibraryReferenceAtRoot, "节点库定义不能是引用")]
    [InlineData(LibraryLeafFrom, "执行节点库定义的接线由引用处提供")]
    [InlineData(UnknownLibraryReference, "不在节点库")]
    [InlineData(UnboundPort, "没有绑定来源")]
    [InlineData(ReferenceWithBody, "引用成员不能同时声明执行方式或子节点")]
    [InlineData(ReferenceWithQuestion, "引用节点不能声明类型参数 Question")]
    [InlineData(ContainerWithQuestion, "Container 节点不能声明参数 Question")]
    [InlineData(UnknownPortBinding, "不在容器")]
    [InlineData(PortNotDeclared, "没有在此容器上声明")]
    [InlineData(ReferenceContainerFrom, "引用容器不能声明 From")]
    [InlineData(AssemblyContainerInputs, "端口声明属于节点库容器定义")]
    [InlineData(DuplicateMemberNames, "在容器子树内重复")]
    [InlineData(LibraryLeafIn, "执行节点不写输入端口绑定")]
    [InlineData(ContainerWithFrom, "不支持 From")]
    [InlineData(ReferenceWithInputs, "引用节点不能声明输入端口")]
    [InlineData(LeafWithInputs, "必须至少给出一组 From")]
    [InlineData(ContainerWithExecutionFields, "容器节点不是执行节点，不支持模型选择与分支")]
    [InlineData(LibraryLeafWithModel, "库执行定义不声明模型")]
    [InlineData(LibraryMemberWithoutSlot, "必须声明模型槽位")]
    [InlineData(MissingModelBinding, "没有外部绑定")]
    [InlineData(ContainerReferenceWithModel, "引用节点组不能声明模型")]
    [InlineData(LeafReferenceWithModelBindings, "引用执行节点不能声明模型绑定")]
    [InlineData(GroupReferenceWithFrom, "引用容器不能声明 From")]
    [InlineData(GroupReferenceWithValidate, "引用节点组不能声明 Validate")]
    [InlineData(MemberFromOutOfScope, "From 引用的节点")]
    [InlineData(MemberGroupOutsideScope, "From 引用的节点")]
    [InlineData(MemberGroupUndeclaredPort, "没有在此容器上声明")]
    [InlineData(LibraryInputWithModel, "不声明模型槽位")]
    [InlineData(LibraryInputWithModels, "不声明模型槽位与模型绑定")]
    [InlineData(LibraryPortOnInput, "最多一个输出端口")]
    [InlineData(LibraryQuestionOnText, "字段 Question 不属于 Llm 节点")]
    [InlineData(LibraryContainerWithOutputs, "容器输出端口不认识键 Type")]
    [InlineData(MemberReferenceWithOutputs, "引用节点不能声明输出端口")]
    [InlineData(LibraryContainerOutMissingMember, "不在容器 交付 的子树里")]
    [InlineData(LibraryContainerOutBadPort, "无法取用")]
    [InlineData(LibraryContainerOutSubContainer, "不是执行节点")]
    [InlineData(LibraryContainerOutInputContext, "不能作为上下文端口来源")]
    [InlineData(LibraryContainerOutItemsPort, "容器不透出 WorkItems 端口")]
    [InlineData(LeafWithOut, "执行节点的输出端口条目写 Name 与 Type，不写 Ref")]
    [InlineData(ReferenceWithOut, "引用节点不能声明输出端口")]
    [InlineData(AssemblyContainerOut, "没有输出端口")]
    [InlineData(LibraryNameWithAt, "节点名不能含 @")]
    [InlineData(MemberNameWithAt, "成员名 甲@乙 不能含 @")]
    [InlineData(LibraryInputPortWithAt, "端口名不能为空或含 @")]
    [InlineData(MemberSlotNotDeclared, "未在容器上声明")]
    [InlineData(UnusedSlotDeclaration, "未被成员使用")]
    [InlineData(GroupBindingMissingSlot, "未绑定模型槽位")]
    [InlineData(GroupBindingUnknownSlot, "不在容器 内组 上")]
    [InlineData(ContainerSlotDeclarationNotArray, "模型槽位写槽位名数组")]
    [InlineData(ReferenceSlotDeclarationArray, "写槽位绑定对象")]
    [InlineData(AssemblyContainerSlotDeclaration, "内联容器不声明模型槽位")]
    [InlineData(LeafSlotDeclaration, "执行节点不声明模型槽位与模型绑定")]
    [InlineData(DuplicateSlotDeclaration, "模型槽位 甲 重复")]
    [InlineData(BlankSlotDeclaration, "模型槽位名不能为空")]
    [InlineData(UnknownSlotBinding, "不在容器 交付 上")]
    public void Invalid_library_fails_loading(string flowsJson, string expected)
    {
        ErrorOr<KuroeHarness> harness = KuroeHarness.TryCreate(flowsJson);

        Assert.True(harness.IsError);
        Assert.Contains(harness.ErrorsOrEmptyList, error => error.Description.Contains(expected));
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
                  "Nodes": [ { "Name": "干活", "Kind": "Llm", "Model": "执行者" } ]
                }
              ]
            }
          ]
        }
        """;

    private const string LibraryFlow = """
        {
          "Nodes": [
            { "Name": "执行", "Kind": "Llm", "Mode": "PerItem" },
            {
              "Name": "交付",
              "Kind": "Container",
              "Models": ["执行者"],
              "Inputs": [{ "Name": "计划" }],
              "Nodes": [
                { "Name": "实施", "Use": "执行", "Model": "执行者", "From": [ { "计划": "@计划" } ] }
              ]
            }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                { "Name": "制定计划", "Kind": "Llm", "Model": "规划者", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] },
                { "Name": "交付", "Use": "交付", "In": { "计划": "制定计划@拆分" }, "Models": { "执行者": "执行者" } }
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
              "Models": ["执行者"],
              "Inputs": [{ "Name": "计划" }],
              "Nodes": [
                { "Name": "实施", "Use": "干活库", "Model": "执行者", "From": [ { "结论": "外来@结论" } ] }
              ]
            }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者" } ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "交付组", "Use": "交付", "Models": { "执行者": "执行者" } }
                  ]
                }
              ]
            }
          ]
        }
        """;
    private const string DualInstanceFlow = """
        {
          "Nodes": [
            { "Name": "规划", "Kind": "Llm", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }] },
            { "Name": "执行", "Kind": "Llm", "Mode": "PerItem" },
            { "Name": "交付", "Kind": "Container", "Models": ["执行者"], "Inputs": [{ "Name": "计划" }], "Nodes": [
              { "Name": "实施", "Use": "执行", "Model": "执行者", "From": [ { "计划": "@计划" } ] }
            ] }
          ],
          "Flows": [
            {
              "Name": "双线",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                { "Name": "制定A", "Use": "规划", "Model": "规划者", "From": [ { "目标": "Start@目标" } ] },
                { "Name": "制定B", "Use": "规划", "Model": "规划者", "From": [ { "目标": "Start@目标" } ] },
                { "Name": "交付A", "Use": "交付", "In": { "计划": "制定A@拆分" }, "Models": { "执行者": "执行者" } },
                { "Name": "交付B", "Use": "交付", "In": { "计划": "制定B@拆分" }, "Models": { "执行者": "执行者" } }
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
            { "Name": "执行", "Kind": "Llm", "Mode": "PerItem" },
            { "Name": "交付", "Kind": "Container", "Models": ["执行者"], "Inputs": [{ "Name": "计划" }], "Nodes": [
              { "Name": "实施", "Use": "执行", "Model": "执行者", "From": [ { "计划": "@计划" } ] }
            ] }
          ],
          "Flows": [
            {
              "Name": "甲",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [ { "Name": "整体", "Kind": "Container", "Nodes": [
                { "Name": "制定计划", "Kind": "Llm", "Model": "规划者", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] },
                { "Name": "交付", "Use": "交付", "In": { "计划": "制定计划@拆分" }, "Models": { "执行者": "执行者" } }
              ] } ]
            },
            {
              "Name": "乙",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "实施者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [ { "Name": "整体", "Kind": "Container", "Nodes": [
                { "Name": "制定计划", "Kind": "Llm", "Model": "规划者", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] },
                { "Name": "交付", "Use": "交付", "In": { "计划": "制定计划@拆分" }, "Models": { "执行者": "实施者" } }
              ] } ]
            }
          ]
        }
        """;

    private const string LibraryDuplicateName = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm", "Mode": "PerItem" },
          { "Name": "执行", "Kind": "Llm", "Mode": "PerItem" }
        ], "Flows": [] }
        """;

    private const string LibraryReferenceAtRoot = """
        { "Nodes": [ { "Name": "执行", "Kind": "Llm", "Mode": "PerItem" }, { "Name": "副本", "Use": "执行" } ], "Flows": [] }
        """;

    private const string LibraryLeafFrom = """
        { "Nodes": [ { "Name": "执行", "Kind": "Llm", "Mode": "PerItem", "From": [ { "拆分": "制定计划@拆分" } ] } ], "Flows": [] }
        """;

    private const string UnknownLibraryReference = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm", "Mode": "PerItem" },
          { "Name": "交付", "Kind": "Container", "Models": ["执行者"], "Nodes": [ { "Name": "实施", "Use": "不存在", "Model": "执行者" } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "Models": { "执行者": "执行者" } } ] }
        ] }
        """;

    private const string UnknownReferenceWithoutModel = """
        { "Nodes": [ { "Name": "执行", "Kind": "Llm", "Mode": "PerItem" } ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Kind": "Container", "Nodes": [ { "Name": "实施", "Use": "不存在" } ] } ] }
        ] }
        """;

    private const string UnboundPort = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm", "Mode": "PerItem" },
          { "Name": "交付", "Kind": "Container", "Models": ["执行者"], "Inputs": [{ "Name": "计划" }], "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "执行者", "From": [ { "计划": "@计划" } ] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "Models": { "执行者": "执行者" } } ] }
        ] }
        """;

    private const string PortNotDeclared = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm", "Mode": "PerItem" },
          { "Name": "交付", "Kind": "Container", "Models": ["执行者"], "Inputs": [{ "Name": "计划" }], "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "执行者", "From": [ { "来源": "@来源" } ] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "Models": { "执行者": "执行者" } } ] }
        ] }
        """;

    private const string ReferenceWithBody = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm", "Mode": "PerItem" },
          { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "计划" }], "Nodes": [ { "Name": "实施", "Use": "执行", "From": [ { "计划": "@计划" } ], "Mode": "PerItem" } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "In": { "计划": "制定计划@拆分" }, "Models": { "执行者": "执行者" } } ] }
        ] }
        """;

    private const string UnknownPortBinding = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm", "Mode": "PerItem" },
          { "Name": "交付", "Kind": "Container", "Models": ["执行者"], "Inputs": [{ "Name": "计划" }], "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "执行者", "From": [ { "计划": "@计划" } ] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "In": { "来源": "制定计划@拆分" }, "Models": { "执行者": "执行者" } } ] }
        ] }
        """;

    private const string LibraryLeafIn = """
        { "Nodes": [ { "Name": "执行", "Kind": "Llm", "Mode": "PerItem", "In": { "x": "y" } } ], "Flows": [] }
        """;

    private const string ContainerWithFrom = """
        { "Nodes": [ { "Name": "组", "Kind": "Container", "Models": ["执行者"], "From": [ { "x": "x" } ], "Nodes": [ { "Name": "实施", "Kind": "Llm", "Model": "执行者" } ] } ], "Flows": [] }
        """;

    private const string ReferenceWithInputs = """
        { "Nodes": [ { "Name": "执行", "Kind": "Llm", "Mode": "PerItem" } ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [ { "Name": "整体", "Kind": "Container", "Nodes": [ { "Name": "实施", "Use": "执行", "Inputs": [{ "Name": "计划" }] } ] } ] }
        ] }
        """;

    private const string LeafWithInputs = """
        { "Nodes": [], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [ { "Name": "整体", "Kind": "Container", "Nodes": [ { "Name": "实施", "Kind": "Llm", "Model": "执行者", "Inputs": [{ "Name": "计划" }] } ] } ] }
        ] }
        """;

    private const string ReferenceWithQuestion = """
        { "Nodes": [ { "Name": "执行", "Kind": "Llm", "Mode": "PerItem" } ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [ { "Name": "整体", "Kind": "Container", "Nodes": [ { "Name": "实施", "Use": "执行", "Question": "确认" } ] } ] }
        ] }
        """;

    private const string ContainerWithQuestion = """
        { "Nodes": [], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [ { "Name": "整体", "Kind": "Container", "Question": "确认", "Nodes": [ { "Name": "实施", "Kind": "Llm", "Model": "执行者" } ] } ] }
        ] }
        """;

    private const string ContainerWithExecutionFields = """
        { "Nodes": [ { "Name": "执行", "Kind": "Llm", "Mode": "PerItem" } ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [ { "Name": "组", "Kind": "Container", "Model": "执行者", "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "执行者" } ] } ] }
        ] }
        """;

    private const string ReferenceContainerFrom = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm", "Mode": "PerItem" },
          { "Name": "交付", "Kind": "Container", "Models": ["执行者"], "Inputs": [{ "Name": "计划" }], "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "执行者", "From": [ { "计划": "@计划" } ] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "Models": { "执行者": "执行者" }, "From": [ { "拆分": "制定计划@拆分" } ] } ] }
        ] }
        """;

    private const string AssemblyContainerInputs = """
        { "Nodes": [], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "组", "Kind": "Container", "Inputs": [{ "Name": "计划" }], "Nodes": [ { "Name": "实施", "Kind": "Llm", "Model": "执行者" } ] } ] }
        ] }
        """;

    private const string DuplicateMemberNames = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm", "Mode": "PerItem" },
          { "Name": "组", "Kind": "Container", "Models": ["执行者"], "Nodes": [
            { "Name": "实施", "Kind": "Llm", "Model": "执行者" },
            { "Name": "内", "Kind": "Container", "Nodes": [ { "Name": "实施", "Kind": "Llm", "Model": "执行者" } ] }
          ] }
        ], "Flows": [] }
        """;

    private const string LibraryLeafWithModel = """
        { "Nodes": [ { "Name": "执行", "Kind": "Llm", "Model": "执行者", "Mode": "PerItem" } ], "Flows": [] }
        """;

    private const string LibraryMemberWithoutSlot = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm", "Mode": "PerItem" },
          { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "计划" }], "Nodes": [ { "Name": "实施", "Use": "执行", "From": [ { "计划": "@计划" } ] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "Models": { "执行者": "执行者" } } ] }
        ] }
        """;

    private const string MissingModelBinding = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm", "Mode": "PerItem" },
          { "Name": "交付", "Kind": "Container", "Models": ["执行者"], "Inputs": [{ "Name": "计划" }], "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "执行者", "From": [ { "计划": "@计划" } ] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付" } ] }
        ] }
        """;

    private const string ContainerReferenceWithModel = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm", "Mode": "PerItem" },
          { "Name": "交付", "Kind": "Container", "Models": ["执行者"], "Inputs": [{ "Name": "计划" }], "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "执行者", "From": [ { "计划": "@计划" } ] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "Model": "执行者" } ] }
        ] }
        """;

    private const string LeafReferenceWithModelBindings = """
        { "Nodes": [ { "Name": "执行", "Kind": "Llm", "Mode": "PerItem" } ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [ { "Name": "整体", "Kind": "Container", "Nodes": [ { "Name": "实施", "Use": "执行", "Models": { "执行者": "执行者" } } ] } ] }
        ] }
        """;

    private const string OuterPortBindingFlow = """
        {
          "Nodes": [
            { "Name": "执行", "Kind": "Llm", "Mode": "PerItem" },
            { "Name": "内层", "Kind": "Container", "Models": ["执行者"], "Inputs": [{ "Name": "内部" }], "Nodes": [
              { "Name": "实施", "Use": "执行", "Model": "执行者", "From": [ { "内部": "@内部" } ] }
            ] },
            { "Name": "外层", "Kind": "Container", "Models": ["执行者"], "Inputs": [{ "Name": "目标" }], "Nodes": [
              { "Name": "里", "Use": "内层", "In": { "内部": "@目标" }, "Models": { "执行者": "执行者" } }
            ] }
          ],
          "Flows": [
            {
              "Name": "透传",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                { "Name": "制定计划", "Kind": "Llm", "Model": "规划者", "Prompt": "把目标拆成可独立实施的条目。", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] },
                { "Name": "交付", "Use": "外层", "In": { "目标": "制定计划@拆分" }, "Models": { "执行者": "执行者" } }
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
            { "Name": "交付", "Kind": "Container", "Models": ["执行者"], "Inputs": [{ "Name": "输入" }], "Nodes": [
              { "Name": "实施", "Use": "分析", "Model": "执行者", "From": [ { "输入": "@输入" } ] }
            ] }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "执行者", "Model": "fake" },
                { "Name": "规划者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "来源", "Use": "分析", "Model": "执行者", "From": [ { "目标": "Start@目标" } ] },
                    { "Name": "交付", "Use": "交付", "In": { "输入": "来源@结论" }, "Models": { "执行者": "执行者" } }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private const string DuplicateImportedFlows = """
        { "Flows": [
          { "Name": "重复", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [ { "Name": "实施", "Kind": "Llm", "Model": "执行者", "From": [ { "目标": "Start@目标" } ] } ] },
          { "Name": "重复", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [ { "Name": "实施", "Kind": "Llm", "Model": "执行者", "From": [ { "目标": "Start@目标" } ] } ] }
        ] }
        """;

    /// <summary>只写流程，引用的节点库定义不在文件里，按文件自身装配会报缺定义。</summary>
    private const string LibraryReferenceOnlyFlow = """
        { "Flows": [
          { "Name": "复用", "Models": [{ "Name": "执行者" }], "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "干活", "Use": "外部库", "Model": "执行者" }
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
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                { "Name": "整体", "Kind": "Container", "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "执行者", "From": [ { "目标": "Start@目标" } ] } ] }
              ]
            }
          ]
        }
        """;

    /// <summary>文件只定义自己的节点，流程同时引用内置「交付」容器。</summary>
    private const string BuiltinReferenceFlow = """
        {
          "Nodes": [
            { "Name": "准备", "Kind": "Llm", "Prompt": "拆条目", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }] }
          ],
          "Flows": [
            {
              "Name": "内置引用",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "定计划", "Use": "准备", "Model": "规划者", "From": [ { "目标": "Start@目标" } ] },
                    { "Name": "交付", "Use": "交付", "In": { "计划": "定计划@拆分" }, "Models": { "执行者": "执行者" } }
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
          { "Name": "额外", "Models": [{ "Name": "执行者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "干活", "Kind": "Llm", "Model": "执行者", "From": [ { "目标": "Start@目标" } ] }
            ] }
          ] }
        ] }
        """;

    /// <summary>一条与默认流程同名的流程，可独立装配。</summary>
    private const string OverwritingFlow = """
        { "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "干活", "Kind": "Llm", "Model": "执行者", "From": [ { "目标": "Start@目标" } ] }
            ] }
          ] }
        ] }
        """;

    private const string MemberNameBindingFlow = """
        {
          "Nodes": [
            { "Name": "执行", "Kind": "Llm", "Mode": "Single" },
            { "Name": "内层", "Kind": "Container", "Models": ["执行者"], "Inputs": [{ "Name": "内部" }], "Nodes": [
              { "Name": "实施", "Use": "执行", "Model": "执行者", "From": [ { "内部": "@内部" } ] }
            ] },
            { "Name": "外层", "Kind": "Container", "Models": ["执行者"], "Inputs": [{ "Name": "目标" }, { "Name": "入口" }], "Nodes": [
              { "Name": "目标条目", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "入口": "@入口" } ] },
              { "Name": "里", "Use": "内层", "In": { "内部": "目标条目@结论" }, "Models": { "执行者": "执行者" } }
            ] }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                { "Name": "规划", "Kind": "Llm", "Model": "规划者", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] },
                { "Name": "交付", "Use": "外层", "In": { "目标": "规划@拆分", "入口": "Start@目标" }, "Models": { "执行者": "执行者" } }
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
        using KuroeHarness harness = KuroeHarness.Create(DocumentExampleFlow);

        FlowDefinition flow = harness.Flows.Find(new FlowName("单线交付")).ThrowIfError();
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
              "Tools": ["GetLocalTime"],
              "Mode": "PerItem"
            },
            {
              "Name": "交付",
              "Kind": "Container",
              "Models": ["执行者"],
              "Inputs": [{ "Name": "计划" }],
              "Nodes": [
                { "Name": "实施", "Use": "允许工具", "Model": "执行者", "From": [ { "计划": "@计划" } ] }
              ]
            }
          ],
          "Flows": [
            {
              "Name": "单线交付",
              "Description": "制定计划、复用交付",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                { "Name": "制定计划", "Kind": "Llm", "Model": "规划者", "Prompt": "把目标拆成可独立实施的条目。", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] },
                { "Name": "交付", "Use": "交付", "In": { "计划": "制定计划@拆分" }, "Models": { "执行者": "执行者" } }
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
              "Mode": "Single",
              "Outputs": [{ "Name": "结论", "Type": "Text" }],
              "Validate": { "Predicate": "TextContains", "Argument": "通过" }
            }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "计划", "Kind": "Llm", "Model": "规划者", "Outputs": [{ "Name": "上下文", "Type": "Context" }], "From": [ { "目标": "Start@目标" } ] },
                    { "Name": "审查实例", "Use": "审查", "Model": "执行者",
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
              "Mode": "Single",
              "Outputs": [{ "Name": "结论", "Type": "Text" }],
              "Validate": { "Predicate": "TextContains", "Argument": "通过" }
            }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "计划", "Kind": "Llm", "Model": "规划者", "From": [ { "目标": "Start@目标" } ] },
                    { "Name": "审查实例", "Use": "审查", "Model": "执行者", "From": [ { "目标": "Start@目标" } ],
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
            { "Name": "审查", "Kind": "Llm", "Mode": "Single" },
            {
              "Name": "组",
              "Kind": "Container",
              "Models": ["执行者"],
              "Nodes": [ { "Name": "甲", "Use": "审查", "Model": "执行者" } ]
            }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "计划", "Kind": "Llm", "Model": "执行者" },
                    { "Name": "引用组", "Use": "组", "From": [ { "结论": "计划@结论" } ] }
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
            { "Name": "审查", "Kind": "Llm", "Mode": "Single" },
            {
              "Name": "组",
              "Kind": "Container",
              "Models": ["执行者"],
              "Nodes": [ { "Name": "甲", "Use": "审查", "Model": "执行者" } ]
            }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "计划", "Kind": "Llm", "Model": "执行者" },
                    { "Name": "引用组", "Use": "组", "Validate": { "Predicate": "NonEmpty" } }
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
              "Models": ["执行者"],
              "Nodes": [
                { "Name": "甲", "Kind": "Llm", "Model": "执行者", "From": [ { "结论": "局外@结论" } ] }
              ]
            }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "交付实例", "Use": "交付", "Models": { "执行者": "执行者" } }
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
              "Models": ["执行者"],
              "Nodes": [
                { "Name": "甲", "Kind": "Llm", "Model": "执行者", "From": [ { "计划": "@计划" } ] }
              ]
            }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "交付实例", "Use": "交付", "Models": { "执行者": "执行者" } }
                  ]
                }
              ]
            }
          ]
        }
        """";

    /// <summary>输入成员在节点库声明模型槽位，装配校验拒绝。</summary>
    private const string LibraryInputWithModel = """
        {
          "Nodes": [
            { "Name": "收集", "Kind": "Input", "Model": "执行者" }
          ]
        }
        """;

    /// <summary>输入成员在节点库声明模型绑定，装配校验拒绝。</summary>
    private const string LibraryInputWithModels = """
        {
          "Nodes": [
            { "Name": "收集", "Kind": "Input", "Models": { "执行者": "伪造" } }
          ]
        }
        """;

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
          { "Name": "交付", "Kind": "Container", "Outputs": [{ "Name": "结论", "Type": "Text" }], "Nodes": [ { "Name": "实施", "Kind": "Llm", "Mode": "PerItem" } ] }
        ], "Flows": [] }
        """;

    /// <summary>容器子树里的引用成员声明输出端口，装配校验拒绝。</summary>
    private const string MemberReferenceWithOutputs = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm" },
          { "Name": "交付", "Kind": "Container", "Models": ["执行者"], "Nodes": [
            { "Name": "实施", "Use": "执行", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }] }
          ] }
        ], "Flows": [] }
        """;

    /// <summary>库定义节点名含 @，与端口引用分隔符冲突，装配校验拒绝。</summary>
    private const string LibraryNameWithAt = """
        { "Nodes": [ { "Name": "甲@乙", "Kind": "Llm" } ], "Flows": [] }
        """;

    /// <summary>库容器成员名含 @，与端口引用分隔符冲突，装配校验拒绝。</summary>
    private const string MemberNameWithAt = """
        { "Nodes": [
          { "Name": "交付", "Kind": "Container", "Models": ["执行者"], "Nodes": [ { "Name": "甲@乙", "Kind": "Llm", "Model": "执行者" } ] }
        ], "Flows": [] }
        """;

    /// <summary>库容器输入端口名含 @，装配校验拒绝。</summary>
    private const string LibraryInputPortWithAt = """
        { "Nodes": [
          { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "计划@甲" }], "Nodes": [ { "Name": "实施", "Kind": "Llm", "Mode": "PerItem" } ] }
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

    /// <summary>库容器输出端口绑定子树里的输入节点声明的上下文端口，输入节点不落 run，不能作为上下文端口来源。</summary>
    private const string LibraryContainerOutInputContext = """
        { "Nodes": [
          { "Name": "交付", "Kind": "Container", "Outputs": [{ "Name": "结果", "Ref": "收集@背景" }], "Nodes": [ { "Name": "收集", "Kind": "Input", "Outputs": [{ "Name": "背景", "Type": "Context" }] } ] }
        ], "Flows": [] }
        """;

    /// <summary>库容器输出端口绑定成员的 WorkItems 端口，容器不透出 WorkItems 端口，装配校验拒绝。</summary>
    private const string LibraryContainerOutItemsPort = """
        { "Nodes": [
          { "Name": "交付", "Kind": "Container", "Outputs": [{ "Name": "结果", "Ref": "计划@拆分" }], "Nodes": [ { "Name": "计划", "Kind": "Llm", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }] } ] }
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
          { "Name": "交付", "Kind": "Container", "Models": ["执行者"], "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "执行者", "Outputs": [{ "Name": "结论", "Ref": "实施" }] } ] }
        ], "Flows": [] }
        """;

    /// <summary>装配层内联容器声明输出端口，绑定目标端口必须被成员声明。</summary>
    private const string AssemblyContainerOut = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Kind": "Container", "Outputs": [{ "Name": "结论", "Ref": "实施@不存在" }], "Nodes": [ { "Name": "实施", "Kind": "Llm", "Model": "执行者" } ] }
        ] } ] }
        """;

    /// <summary>库容器成员写的模型槽位不在容器声明里，装配校验拒绝。</summary>
    private const string MemberSlotNotDeclared = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm" },
          { "Name": "交付", "Kind": "Container", "Models": ["甲"], "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "乙" } ] }
        ], "Flows": [] }
        """;

    /// <summary>库容器声明的模型槽位没有成员使用，装配校验拒绝。</summary>
    private const string UnusedSlotDeclaration = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm" },
          { "Name": "交付", "Kind": "Container", "Models": ["甲", "乙"], "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "甲" } ] }
        ], "Flows": [] }
        """;

    /// <summary>引用节点组没有绑定被引用定义声明的槽位，装配校验拒绝。</summary>
    private const string GroupBindingMissingSlot = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm" },
          { "Name": "内组", "Kind": "Container", "Models": ["甲"], "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "甲" } ] },
          { "Name": "外组", "Kind": "Container", "Models": ["甲"], "Nodes": [ { "Name": "里", "Use": "内组" } ] }
        ], "Flows": [] }
        """;

    /// <summary>引用节点组绑定了被引用定义没有声明的槽位，装配校验拒绝。</summary>
    private const string GroupBindingUnknownSlot = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm" },
          { "Name": "内组", "Kind": "Container", "Models": ["甲"], "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "甲" } ] },
          { "Name": "外组", "Kind": "Container", "Models": ["甲"], "Nodes": [ { "Name": "里", "Use": "内组", "Models": { "甲": "甲", "乙": "甲" } } ] }
        ], "Flows": [] }
        """;

    /// <summary>容器定义的模型槽位写成绑定对象，装配校验拒绝。</summary>
    private const string ContainerSlotDeclarationNotArray = """
        { "Nodes": [ { "Name": "交付", "Kind": "Container", "Models": { "甲": "乙" }, "Nodes": [ { "Name": "实施", "Kind": "Llm", "Model": "甲" } ] } ], "Flows": [] }
        """;

    /// <summary>引用节点组的模型槽位写成声明数组，装配校验拒绝。</summary>
    private const string ReferenceSlotDeclarationArray = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm" },
          { "Name": "内组", "Kind": "Container", "Models": ["甲"], "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "甲" } ] }
        ], "Flows": [ { "Name": "默认", "Models": [{ "Name": "甲", "Model": "fake" }], "Nodes": [ { "Name": "整体", "Kind": "Container", "Nodes": [ { "Name": "引用组", "Use": "内组", "Models": ["甲"] } ] } ] } ] }
        """;

    /// <summary>装配层内联容器声明模型槽位，装配校验拒绝。</summary>
    private const string AssemblyContainerSlotDeclaration = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "甲", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [ { "Name": "整体", "Kind": "Container", "Models": ["甲"], "Nodes": [ { "Name": "实施", "Kind": "Llm", "Model": "甲", "From": [ { "目标": "Start@目标" } ] } ] } ] } ] }
        """;

    /// <summary>执行节点声明模型槽位，装配校验拒绝。</summary>
    private const string LeafSlotDeclaration = """
        { "Nodes": [ { "Name": "执行", "Kind": "Llm", "Models": ["甲"] } ], "Flows": [] }
        """;

    /// <summary>容器声明重复的模型槽位，装配校验拒绝。</summary>
    private const string DuplicateSlotDeclaration = """
        { "Nodes": [ { "Name": "交付", "Kind": "Container", "Models": ["甲", "甲"], "Nodes": [ { "Name": "实施", "Kind": "Llm", "Model": "甲" } ] } ], "Flows": [] }
        """;

    /// <summary>容器声明空的模型槽位名，装配校验拒绝。</summary>
    private const string BlankSlotDeclaration = """
        { "Nodes": [ { "Name": "交付", "Kind": "Container", "Models": [""], "Nodes": [ { "Name": "实施", "Kind": "Llm", "Model": "甲" } ] } ], "Flows": [] }
        """;

    /// <summary>流程引用容器时绑定声明以外的模型槽位，装配校验拒绝。</summary>
    private const string UnknownSlotBinding = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Llm" },
          { "Name": "交付", "Kind": "Container", "Models": ["甲"], "Inputs": [{ "Name": "计划" }], "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "甲", "From": [ { "计划": "@计划" } ] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [ { "Name": "用", "Use": "交付", "In": { "计划": "Start@目标" }, "Models": { "甲": "执行者", "乙": "执行者" } } ] }
        ] }
        """;

    /// <summary>校验节点库，取错误说明。</summary>
    private static IReadOnlyList<string> LibraryErrors(params NodeSpec[] library)
    {
        NodeLibrary nodeLibrary = new();
        foreach (NodeSpec node in library)
        {
            nodeLibrary.Register(node);
        }

        return [.. NodeLibraryRules.Validate(nodeLibrary, TestNodeTypes.Registry()).ErrorsOrEmptyList.Select(error => error.Description)];
    }
}

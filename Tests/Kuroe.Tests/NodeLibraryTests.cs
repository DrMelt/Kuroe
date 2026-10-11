using ErrorOr;
using Kuroe.Configuration;
using Kuroe.Configuration.Files;
using Kuroe.Nodes.Library;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
using Kuroe.Workflows.Flows;
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
        Assert.Equal(NodeKind.Executable, plan.Kind);
        Assert.Equal([new SourceRef(PortRef.Named(new NodeName("Start"), new PortName("目标")))], plan.From);

        NodeSpec deliver = root.Nodes[1];
        Assert.NotNull(deliver.Nodes);
        NodeSpec implement = deliver.Nodes[0];
        Assert.Equal(new NodeName("交付.实施"), implement.Name);
        Assert.NotNull(implement.Execution);
        Assert.Equal(NodeMode.PerItem, implement.Execution.Mode);
        Assert.Equal([new SourceRef(PortRef.Named(new NodeName("制定计划"), new PortName("拆分")))], implement.From);
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

        var (Nodes, _) = FlowAssembler.Read(KuroePaths.At(harness.Root).FlowsFile).ThrowIfError();
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
            LibraryErrors(new NodeSpec { Name = new NodeName("干活库"), Kind = NodeKind.Executable, Execution = new ExecutableSpec { MaxRuns = 0 } }));

    /// <summary>库容器定义的非正统一上限，未经引用也在库校验阶段拒绝。</summary>
    [Fact]
    public void Library_container_with_non_positive_max_runs_is_rejected() =>
        Assert.Equal(
            ["节点库定义 干活组：MaxRuns 必须是正整数。"],
            LibraryErrors(new NodeSpec
            {
                Name = new NodeName("干活组"),
                Kind = NodeKind.Container,
                MaxRuns = 0,
                Nodes = [new NodeSpec { Name = new NodeName("干活"), Kind = NodeKind.Executable, Model = new ModelRef("执行者"), Execution = new ExecutableSpec() }],
            }));

    /// <summary>库容器定义必须声明子节点，缺子节点在库校验阶段拒绝。</summary>
    [Fact]
    public void Library_container_without_children_is_rejected() =>
        Assert.Equal(
            ["节点库定义 空组：容器节点必须声明子节点。"],
            LibraryErrors(new NodeSpec { Name = new NodeName("空组"), Kind = NodeKind.Container }));

    /// <summary>库执行定义不能声明子节点，子节点只属于容器。</summary>
    [Fact]
    public void Library_executable_with_children_is_rejected() =>
        Assert.Equal(
            ["节点库定义 干活库：执行节点与输入节点不能声明子节点，子节点只属于容器。"],
            LibraryErrors(new NodeSpec
            {
                Name = new NodeName("干活库"),
                Kind = NodeKind.Executable,
                Execution = new ExecutableSpec(),
                Nodes = [new NodeSpec { Name = new NodeName("干活"), Kind = NodeKind.Executable, Execution = new ExecutableSpec() }],
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
                Kind = NodeKind.Container,
                Outputs = [new Port(new PortName("结论"), PortKind.Data)],
                Validate = new OutputValidation(ValidationPredicate.NonEmpty, null),
                Nodes = [new NodeSpec { Name = new NodeName("干活"), Kind = NodeKind.Executable, Model = new ModelRef("执行者"), Execution = new ExecutableSpec() }],
            }));

    /// <summary>库执行定义不写容器输出端口，那属于容器定义。</summary>
    [Fact]
    public void Library_executable_with_container_out_is_rejected() =>
        Assert.Equal(
            ["节点库定义 干活库：库执行定义不声明容器输出端口，执行节点输出端口条目写 Name 与 Kind。"],
            LibraryErrors(new NodeSpec
            {
                Name = new NodeName("干活库"),
                Kind = NodeKind.Executable,
                Execution = new ExecutableSpec(),
                ContainerOutputs = new Dictionary<PortName, ContainerOutput> { [new PortName("结论")] = new(PortRef.Named(new NodeName("干活"), new PortName("结论"))) },
            }));

    /// <summary>引用成员不写 Gate，门控由被引用的库定义决定。</summary>
    [Fact]
    public void Reference_member_with_gate_is_rejected() =>
        Assert.Equal(
            ["节点库定义 干活：引用成员不声明 Gate，门控由被引用定义决定。"],
            LibraryErrors(
                new NodeSpec { Name = new NodeName("干活库"), Kind = NodeKind.Executable, Execution = new ExecutableSpec() },
                new NodeSpec
                {
                    Name = new NodeName("干活组"),
                    Kind = NodeKind.Container,
                    Nodes =
                    [
                        new NodeSpec
                        {
                            Name = new NodeName("干活"),
                            Kind = null,
                            Use = new NodeName("干活库"),
                            Model = new ModelRef("执行者"),
                            Gate = NodeGate.Review,
                        },
                    ],
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

        Assert.Equal("文件覆写的指令", implement.Execution!.Prompt);
    }

    [Fact]
    public void Port_binding_value_resolves_outer_member_name()
    {
        using KuroeHarness harness = KuroeHarness.Create(MemberNameBindingFlow);

        FlowDefinition flow = harness.Flows.Find(new FlowName("默认")).ThrowIfError();

        NodeSpec deliver = flow.RootNode.Nodes![1];
        NodeSpec implement = deliver.Nodes![1].Nodes![0];
        Assert.Equal(new NodeName("交付.里.实施"), implement.Name);
        Assert.Equal([new SourceRef(PortRef.Named(new NodeName("交付.目标条目"), new PortName("结论")))], implement.From);
    }

    [Fact]
    public void Port_binding_value_passes_through_outer_scope()
    {
        using KuroeHarness harness = KuroeHarness.Create(OuterPortBindingFlow);

        FlowDefinition flow = harness.Flows.Find(new FlowName("透传")).ThrowIfError();

        NodeSpec deliver = flow.RootNode.Nodes![1];
        NodeSpec implement = deliver.Nodes![0].Nodes![0];
        Assert.Equal([new SourceRef(PortRef.Named(new NodeName("制定计划"), new PortName("拆分")))], implement.From);
    }

    /// <summary>绑定值带端口选取时端口随绑定展开保留，不退回整份引用。</summary>
    [Fact]
    public void Port_binding_value_keeps_port_selection()
    {
        using KuroeHarness harness = KuroeHarness.Create(PortSelectionBindingFlow);

        FlowDefinition flow = harness.Flows.Find(new FlowName("默认")).ThrowIfError();

        NodeSpec deliver = flow.RootNode.Nodes![1];
        NodeSpec implement = deliver.Nodes![0];
        Assert.Equal(new PortName("结论"), Assert.Single(implement.From).Ref.Port);
        Assert.Equal(new NodeName("来源"), Assert.Single(implement.From).Ref.Source);
        Assert.Equal("来源@结论", Assert.Single(implement.From).Ref.Display);
    }

    [Fact]
    public void Reference_executable_inherits_library_validate_and_injects_from()
    {
        using KuroeHarness harness = KuroeHarness.Create(ReferenceInheritFlow);

        FlowDefinition flow = harness.Flows.Find(new FlowName("默认")).ThrowIfError();
        NodeSpec reviewed = flow.RootNode.Nodes![1];
        Assert.Equal(ValidationPredicate.TextContains, reviewed.Execution!.Validate!.Predicate);
        Assert.Equal(new SourceRef(PortRef.Named(new NodeName("计划"), new PortName("上下文")), "任选"), Assert.Single(reviewed.From));
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
    public void Reference_executable_with_injected_or_group_releases_done()
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
    [InlineData(ReferenceWithBody, "引用成员不能同时声明执行配置或子节点")]
    [InlineData(UnknownPortBinding, "不在容器")]
    [InlineData(PortNotDeclared, "没有在此容器上声明")]
    [InlineData(ReferenceWithGate, "引用节点不能声明 Gate")]
    [InlineData(ReferenceContainerFrom, "引用容器不能声明 From")]
    [InlineData(AssemblyContainerInputs, "端口声明属于节点库容器定义")]
    [InlineData(DuplicateMemberNames, "在容器子树内重复")]
    [InlineData(LibraryLeafIn, "执行节点不写输入端口绑定")]
    [InlineData(ContainerWithFrom, "不支持 From")]
    [InlineData(ReferenceWithInputs, "引用节点不能声明输入端口")]
    [InlineData(LeafWithInputs, "执行节点不能声明输入端口")]
    [InlineData(ContainerWithExecutionFields, "不支持执行配置")]
    [InlineData(LibraryLeafWithModel, "库执行定义不声明模型")]
    [InlineData(LibraryMemberWithoutSlot, "必须声明模型槽位")]
    [InlineData(MissingModelBinding, "没有外部绑定")]
    [InlineData(ContainerReferenceWithModel, "引用节点组不能声明模型")]
    [InlineData(LeafReferenceWithModelBindings, "引用执行节点不能声明模型绑定")]
    [InlineData(GroupReferenceWithFrom, "引用容器不能声明 From")]
    [InlineData(GroupReferenceWithValidate, "引用节点组不能声明 Validate")]
    [InlineData(MemberFromOutOfScope, "From 引用的节点")]
    [InlineData(MemberOrOutsideScope, "From 引用的节点")]
    [InlineData(MemberOrUndeclaredPort, "没有在此容器上声明")]
    [InlineData(LibraryInputWithModel, "不声明模型槽位")]
    [InlineData(LibraryInputWithModels, "不能声明模型绑定")]
    [InlineData(LibraryPortOnInput, "最多一个输出端口")]
    [InlineData(LibraryQuestionOnText, "Question 只属于输入节点")]
    [InlineData(LibraryContainerWithOutputs, "容器输出端口条目写 Name 与 Ref")]
    [InlineData(MemberReferenceWithOutputs, "引用节点不能声明输出端口与系统指令")]
    [InlineData(LibraryContainerOutMissingMember, "不在容器 交付 的子树里")]
    [InlineData(LibraryContainerOutBadPort, "无法取用")]
    [InlineData(LibraryContainerOutSubContainer, "不是执行节点")]
    [InlineData(LibraryContainerOutInputContext, "不能作为上下文端口来源")]
    [InlineData(LibraryContainerOutItemsPort, "容器不透出拆分支票")]
    [InlineData(LeafWithOut, "执行节点的输出端口条目写 Name 与 Kind，不写 Ref")]
    [InlineData(ReferenceWithOut, "引用节点不能声明输出端口")]
    [InlineData(AssemblyContainerOut, "没有输出端口")]
    [InlineData(LibraryNameWithAt, "节点名不能含 @")]
    [InlineData(MemberNameWithAt, "成员名 甲@乙 不能含 @")]
    [InlineData(LibraryInputPortWithAt, "端口名不能为空或含 @")]
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
                  "Nodes": [ { "Name": "干活", "Kind": "Executable", "Model": "执行者" } ]
                }
              ]
            }
          ]
        }
        """;

    private const string LibraryFlow = """
        {
          "Nodes": [
            { "Name": "执行", "Kind": "Executable", "Mode": "PerItem" },
            {
              "Name": "交付",
              "Kind": "Container",
              "Inputs": [{ "Name": "计划" }],
              "Nodes": [
                { "Name": "实施", "Use": "执行", "Model": "执行者", "From": ["@计划"] }
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
              "Start": { "Outputs": [{ "Name": "目标", "Kind": "Data" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                { "Name": "制定计划", "Kind": "Executable", "Model": "规划者", "Outputs": [{ "Name": "拆分", "Kind": "Items" }], "From": ["Start@目标"] },
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
            { "Name": "干活库", "Kind": "Executable" },
            {
              "Name": "交付",
              "Kind": "Container",
              "Inputs": [{ "Name": "计划" }],
              "Nodes": [
                { "Name": "实施", "Use": "干活库", "Model": "执行者", "From": [{ "Node": "外来@结论", "Signal": true }] }
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
            { "Name": "规划", "Kind": "Executable", "Outputs": [{ "Name": "拆分", "Kind": "Items" }] },
            { "Name": "执行", "Kind": "Executable", "Mode": "PerItem" },
            { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "计划" }], "Nodes": [
              { "Name": "实施", "Use": "执行", "Model": "执行者", "From": ["@计划"] }
            ] }
          ],
          "Flows": [
            {
              "Name": "双线",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Kind": "Data" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                { "Name": "制定A", "Use": "规划", "Model": "规划者", "From": ["Start@目标"] },
                { "Name": "制定B", "Use": "规划", "Model": "规划者", "From": ["Start@目标"] },
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
            { "Name": "执行", "Kind": "Executable", "Mode": "PerItem" },
            { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "计划" }], "Nodes": [
              { "Name": "实施", "Use": "执行", "Model": "执行者", "From": ["@计划"] }
            ] }
          ],
          "Flows": [
            {
              "Name": "甲",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Kind": "Data" }] },
              "Nodes": [ { "Name": "整体", "Kind": "Container", "Nodes": [
                { "Name": "制定计划", "Kind": "Executable", "Model": "规划者", "Outputs": [{ "Name": "拆分", "Kind": "Items" }], "From": ["Start@目标"] },
                { "Name": "交付", "Use": "交付", "In": { "计划": "制定计划@拆分" }, "Models": { "执行者": "执行者" } }
              ] } ]
            },
            {
              "Name": "乙",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "实施者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Kind": "Data" }] },
              "Nodes": [ { "Name": "整体", "Kind": "Container", "Nodes": [
                { "Name": "制定计划", "Kind": "Executable", "Model": "规划者", "Outputs": [{ "Name": "拆分", "Kind": "Items" }], "From": ["Start@目标"] },
                { "Name": "交付", "Use": "交付", "In": { "计划": "制定计划@拆分" }, "Models": { "执行者": "实施者" } }
              ] } ]
            }
          ]
        }
        """;

    private const string LibraryDuplicateName = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Executable", "Mode": "PerItem" },
          { "Name": "执行", "Kind": "Executable", "Mode": "PerItem" }
        ], "Flows": [] }
        """;

    private const string LibraryReferenceAtRoot = """
        { "Nodes": [ { "Name": "执行", "Kind": "Executable", "Mode": "PerItem" }, { "Name": "副本", "Use": "执行" } ], "Flows": [] }
        """;

    private const string LibraryLeafFrom = """
        { "Nodes": [ { "Name": "执行", "Kind": "Executable", "Mode": "PerItem", "From": ["制定计划@拆分"] } ], "Flows": [] }
        """;

    private const string UnknownLibraryReference = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Executable", "Mode": "PerItem" },
          { "Name": "交付", "Kind": "Container", "Nodes": [ { "Name": "实施", "Use": "不存在", "Model": "执行者" } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "Models": { "执行者": "执行者" } } ] }
        ] }
        """;

    private const string UnknownReferenceWithoutModel = """
        { "Nodes": [ { "Name": "执行", "Kind": "Executable", "Mode": "PerItem" } ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Kind": "Container", "Nodes": [ { "Name": "实施", "Use": "不存在" } ] } ] }
        ] }
        """;

    private const string UnboundPort = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Executable", "Mode": "PerItem" },
          { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "计划" }], "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "执行者", "From": ["@计划"] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "Models": { "执行者": "执行者" } } ] }
        ] }
        """;

    private const string PortNotDeclared = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Executable", "Mode": "PerItem" },
          { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "计划" }], "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "执行者", "From": ["@来源"] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "Models": { "执行者": "执行者" } } ] }
        ] }
        """;

    private const string ReferenceWithBody = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Executable", "Mode": "PerItem" },
          { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "计划" }], "Nodes": [ { "Name": "实施", "Use": "执行", "From": ["@计划"], "Mode": "PerItem" } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "In": { "计划": "制定计划@拆分" }, "Models": { "执行者": "执行者" } } ] }
        ] }
        """;

    private const string UnknownPortBinding = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Executable", "Mode": "PerItem" },
          { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "计划" }], "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "执行者", "From": ["@计划"] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "In": { "来源": "制定计划@拆分" }, "Models": { "执行者": "执行者" } } ] }
        ] }
        """;

    private const string LibraryLeafIn = """
        { "Nodes": [ { "Name": "执行", "Kind": "Executable", "Mode": "PerItem", "In": { "x": "y" } } ], "Flows": [] }
        """;

    private const string ContainerWithFrom = """
        { "Nodes": [ { "Name": "组", "Kind": "Container", "From": ["x"], "Nodes": [ { "Name": "实施", "Kind": "Executable", "Model": "执行者" } ] } ], "Flows": [] }
        """;

    private const string ReferenceWithInputs = """
        { "Nodes": [ { "Name": "执行", "Kind": "Executable", "Mode": "PerItem" } ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [ { "Name": "整体", "Kind": "Container", "Nodes": [ { "Name": "实施", "Use": "执行", "Inputs": [{ "Name": "计划" }] } ] } ] }
        ] }
        """;

    private const string LeafWithInputs = """
        { "Nodes": [], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [ { "Name": "整体", "Kind": "Container", "Nodes": [ { "Name": "实施", "Kind": "Executable", "Model": "执行者", "Inputs": [{ "Name": "计划" }] } ] } ] }
        ] }
        """;

    private const string ContainerWithExecutionFields = """
        { "Nodes": [ { "Name": "执行", "Kind": "Executable", "Mode": "PerItem" } ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [ { "Name": "组", "Kind": "Container", "Model": "执行者", "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "执行者" } ] } ] }
        ] }
        """;

    private const string ReferenceWithGate = """
        { "Nodes": [ { "Name": "执行", "Kind": "Executable", "Mode": "PerItem" } ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [ { "Name": "整体", "Kind": "Container", "Nodes": [ { "Name": "实施", "Use": "执行", "Gate": "Review" } ] } ] }
        ] }
        """;

    private const string ReferenceContainerFrom = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Executable", "Mode": "PerItem" },
          { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "计划" }], "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "执行者", "From": ["@计划"] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "Models": { "执行者": "执行者" }, "From": ["制定计划@拆分"] } ] }
        ] }
        """;

    private const string AssemblyContainerInputs = """
        { "Nodes": [], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "组", "Kind": "Container", "Inputs": [{ "Name": "计划" }], "Nodes": [ { "Name": "实施", "Kind": "Executable", "Model": "执行者" } ] } ] }
        ] }
        """;

    private const string DuplicateMemberNames = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Executable", "Mode": "PerItem" },
          { "Name": "组", "Kind": "Container", "Nodes": [
            { "Name": "实施", "Kind": "Executable", "Model": "执行者" },
            { "Name": "内", "Kind": "Container", "Nodes": [ { "Name": "实施", "Kind": "Executable", "Model": "执行者" } ] }
          ] }
        ], "Flows": [] }
        """;

    private const string LibraryLeafWithModel = """
        { "Nodes": [ { "Name": "执行", "Kind": "Executable", "Model": "执行者", "Mode": "PerItem" } ], "Flows": [] }
        """;

    private const string LibraryMemberWithoutSlot = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Executable", "Mode": "PerItem" },
          { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "计划" }], "Nodes": [ { "Name": "实施", "Use": "执行", "From": ["@计划"] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "Models": { "执行者": "执行者" } } ] }
        ] }
        """;

    private const string MissingModelBinding = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Executable", "Mode": "PerItem" },
          { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "计划" }], "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "执行者", "From": ["@计划"] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付" } ] }
        ] }
        """;

    private const string ContainerReferenceWithModel = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Executable", "Mode": "PerItem" },
          { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "计划" }], "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "执行者", "From": ["@计划"] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "Model": "执行者" } ] }
        ] }
        """;

    private const string LeafReferenceWithModelBindings = """
        { "Nodes": [ { "Name": "执行", "Kind": "Executable", "Mode": "PerItem" } ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [ { "Name": "整体", "Kind": "Container", "Nodes": [ { "Name": "实施", "Use": "执行", "Models": { "执行者": "执行者" } } ] } ] }
        ] }
        """;

    private const string OuterPortBindingFlow = """
        {
          "Nodes": [
            { "Name": "执行", "Kind": "Executable", "Mode": "PerItem" },
            { "Name": "内层", "Kind": "Container", "Inputs": [{ "Name": "内部" }], "Nodes": [
              { "Name": "实施", "Use": "执行", "Model": "执行者", "From": ["@内部"] }
            ] },
            { "Name": "外层", "Kind": "Container", "Inputs": [{ "Name": "目标" }], "Nodes": [
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
              "Start": { "Outputs": [{ "Name": "目标", "Kind": "Data" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                { "Name": "制定计划", "Kind": "Executable", "Model": "规划者", "Prompt": "把目标拆成可独立实施的条目。", "Outputs": [{ "Name": "拆分", "Kind": "Items" }], "From": ["Start@目标"] },
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
            { "Name": "分析", "Kind": "Executable", "Outputs": [{ "Name": "结论", "Kind": "Data" }] },
            { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "输入" }], "Nodes": [
              { "Name": "实施", "Use": "分析", "Model": "执行者", "From": ["@输入"] }
            ] }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "执行者", "Model": "fake" },
                { "Name": "规划者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Kind": "Data" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "来源", "Use": "分析", "Model": "执行者", "From": ["Start@目标"] },
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
          { "Name": "重复", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Kind": "Data" }] }, "Nodes": [ { "Name": "实施", "Kind": "Executable", "Model": "执行者", "From": ["Start@目标"] } ] },
          { "Name": "重复", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Kind": "Data" }] }, "Nodes": [ { "Name": "实施", "Kind": "Executable", "Model": "执行者", "From": ["Start@目标"] } ] }
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
            { "Name": "执行", "Kind": "Executable", "Prompt": "文件覆写的指令" }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Start": { "Outputs": [{ "Name": "目标", "Kind": "Data" }] },
              "Nodes": [
                { "Name": "整体", "Kind": "Container", "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "执行者", "From": ["Start@目标"] } ] }
              ]
            }
          ]
        }
        """;

    /// <summary>文件只定义自己的节点，流程同时引用内置「交付」容器。</summary>
    private const string BuiltinReferenceFlow = """
        {
          "Nodes": [
            { "Name": "准备", "Kind": "Executable", "Prompt": "拆条目", "Outputs": [{ "Name": "拆分", "Kind": "Items" }] }
          ],
          "Flows": [
            {
              "Name": "内置引用",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Kind": "Data" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "定计划", "Use": "准备", "Model": "规划者", "From": ["Start@目标"] },
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
          { "Name": "额外", "Models": [{ "Name": "执行者" }], "Start": { "Outputs": [{ "Name": "目标", "Kind": "Data" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "干活", "Kind": "Executable", "Model": "执行者", "From": ["Start@目标"] }
            ] }
          ] }
        ] }
        """;

    /// <summary>一条与默认流程同名的流程，可独立装配。</summary>
    private const string OverwritingFlow = """
        { "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Start": { "Outputs": [{ "Name": "目标", "Kind": "Data" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "干活", "Kind": "Executable", "Model": "执行者", "From": ["Start@目标"] }
            ] }
          ] }
        ] }
        """;

    private const string MemberNameBindingFlow = """
        {
          "Nodes": [
            { "Name": "执行", "Kind": "Executable", "Mode": "Single" },
            { "Name": "内层", "Kind": "Container", "Inputs": [{ "Name": "内部" }], "Nodes": [
              { "Name": "实施", "Use": "执行", "Model": "执行者", "From": ["@内部"] }
            ] },
            { "Name": "外层", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "入口" }], "Nodes": [
              { "Name": "目标条目", "Kind": "Executable", "Model": "执行者", "Outputs": [{ "Name": "结论", "Kind": "Data" }], "From": ["@入口"] },
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
              "Start": { "Outputs": [{ "Name": "目标", "Kind": "Data" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                { "Name": "规划", "Kind": "Executable", "Model": "规划者", "Outputs": [{ "Name": "拆分", "Kind": "Items" }], "From": ["Start@目标"] },
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
              "Kind": "Executable",
              "Tools": ["GetLocalTime"],
              "Mode": "PerItem"
            },
            {
              "Name": "交付",
              "Kind": "Container",
              "Inputs": [{ "Name": "计划" }],
              "Nodes": [
                { "Name": "实施", "Use": "允许工具", "Model": "执行者", "From": ["@计划"] }
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
              "Start": { "Outputs": [{ "Name": "目标", "Kind": "Data" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                { "Name": "制定计划", "Kind": "Executable", "Model": "规划者", "Prompt": "把目标拆成可独立实施的条目。", "Outputs": [{ "Name": "拆分", "Kind": "Items" }], "From": ["Start@目标"] },
                { "Name": "交付", "Use": "交付", "In": { "计划": "制定计划@拆分" }, "Models": { "执行者": "执行者" } }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>库执行定义带校验，引用处注入可选启动组与数据接线。</summary>
    private const string ReferenceInheritFlow = """
        {
          "Nodes": [
            {
              "Name": "审查",
              "Kind": "Executable",
              "Mode": "Single",
              "Outputs": [{ "Name": "结论", "Kind": "Data" }],
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
              "Start": { "Outputs": [{ "Name": "目标", "Kind": "Data" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "计划", "Kind": "Executable", "Model": "规划者", "Outputs": [{ "Name": "上下文", "Kind": "Context" }], "From": ["Start@目标"] },
                    { "Name": "审查实例", "Use": "审查", "Model": "执行者",
                      "From": [{ "Node": "计划@上下文", "Or": "任选" }] }
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
              "Kind": "Executable",
              "Mode": "Single",
              "Outputs": [{ "Name": "结论", "Kind": "Data" }],
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
              "Start": { "Outputs": [{ "Name": "目标", "Kind": "Data" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "计划", "Kind": "Executable", "Model": "规划者", "From": ["Start@目标"] },
                    { "Name": "审查实例", "Use": "审查", "Model": "执行者", "From": ["Start@目标"],
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
            { "Name": "审查", "Kind": "Executable", "Mode": "Single" },
            {
              "Name": "组",
              "Kind": "Container",
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
                    { "Name": "计划", "Kind": "Executable", "Model": "执行者" },
                    { "Name": "引用组", "Use": "组", "From": [{ "Node": "计划@结论", "Or": "任选" }] }
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
            { "Name": "审查", "Kind": "Executable", "Mode": "Single" },
            {
              "Name": "组",
              "Kind": "Container",
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
                    { "Name": "计划", "Kind": "Executable", "Model": "执行者" },
                    { "Name": "引用组", "Use": "组", "Validate": { "Predicate": "NonEmpty" } }
                  ]
                }
              ]
            }
          ]
        }
        """";

    /// <summary>库容器成员执行节点的可选组来源必须落在容器作用域内。</summary>
    private const string MemberOrOutsideScope = """"
        {
          "Nodes": [
            {
              "Name": "交付",
              "Kind": "Container",
              "Nodes": [
                { "Name": "甲", "Kind": "Executable", "Model": "执行者", "From": [{ "Node": "局外@结论", "Or": "任选" }] }
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

    /// <summary>库容器成员执行节点的可选组端口必须已由容器定义声明。</summary>
    private const string MemberOrUndeclaredPort = """"
        {
          "Nodes": [
            {
              "Name": "交付",
              "Kind": "Container",
              "Nodes": [
                { "Name": "甲", "Kind": "Executable", "Model": "执行者", "From": [{ "Node": "@计划", "Or": "任选" }] }
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
            { "Name": "收集", "Kind": "Input", "Outputs": [{ "Name": "结论", "Kind": "Data" }, { "Name": "理由", "Kind": "Data" }] }
          ]
        }
        """;

    /// <summary>库执行定义声明 Question，装配校验拒绝。</summary>
    private const string LibraryQuestionOnText = """
        { "Nodes": [ { "Name": "干活库", "Kind": "Executable", "Question": "你想问什么" } ], "Flows": [] }
        """;

    /// <summary>库容器定义声明输出端口，装配校验拒绝。</summary>
    private const string LibraryContainerWithOutputs = """
        { "Nodes": [
          { "Name": "交付", "Kind": "Container", "Outputs": [{ "Name": "结论", "Kind": "Data" }], "Nodes": [ { "Name": "实施", "Kind": "Executable", "Mode": "PerItem" } ] }
        ], "Flows": [] }
        """;

    /// <summary>容器子树里的引用成员声明输出端口，装配校验拒绝。</summary>
    private const string MemberReferenceWithOutputs = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Executable" },
          { "Name": "交付", "Kind": "Container", "Nodes": [
            { "Name": "实施", "Use": "执行", "Model": "执行者", "Outputs": [{ "Name": "结论", "Kind": "Data" }] }
          ] }
        ], "Flows": [] }
        """;

    /// <summary>库定义节点名含 @，与端口引用分隔符冲突，装配校验拒绝。</summary>
    private const string LibraryNameWithAt = """
        { "Nodes": [ { "Name": "甲@乙", "Kind": "Executable" } ], "Flows": [] }
        """;

    /// <summary>库容器成员名含 @，与端口引用分隔符冲突，装配校验拒绝。</summary>
    private const string MemberNameWithAt = """
        { "Nodes": [
          { "Name": "交付", "Kind": "Container", "Nodes": [ { "Name": "甲@乙", "Kind": "Executable", "Model": "执行者" } ] }
        ], "Flows": [] }
        """;

    /// <summary>库容器输入端口名含 @，装配校验拒绝。</summary>
    private const string LibraryInputPortWithAt = """
        { "Nodes": [
          { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "计划@甲" }], "Nodes": [ { "Name": "实施", "Kind": "Executable", "Mode": "PerItem" } ] }
        ], "Flows": [] }
        """;

    /// <summary>库容器输出端口绑定目标不在容器子树内，装配校验拒绝。</summary>
    private const string LibraryContainerOutMissingMember = """
        { "Nodes": [
          { "Name": "交付", "Kind": "Container", "Outputs": [{ "Name": "结论", "Ref": "外部@结论" }], "Nodes": [ { "Name": "实施", "Kind": "Executable" } ] }
        ], "Flows": [] }
        """;

    /// <summary>库容器输出端口绑定目标端口未声明，装配校验拒绝。</summary>
    private const string LibraryContainerOutBadPort = """
        { "Nodes": [
          {
            "Name": "交付",
            "Kind": "Container",
            "Outputs": [{ "Name": "结论", "Ref": "实施@不存在" }],
            "Nodes": [ { "Name": "实施", "Kind": "Executable", "Outputs": [{ "Name": "结论", "Kind": "Data" }] } ]
          }
        ], "Flows": [] }
        """;

    /// <summary>库容器输出端口绑定子树里的输入节点声明的上下文端口，输入节点不落 run，不能作为上下文端口来源。</summary>
    private const string LibraryContainerOutInputContext = """
        { "Nodes": [
          { "Name": "交付", "Kind": "Container", "Outputs": [{ "Name": "结果", "Ref": "收集@背景" }], "Nodes": [ { "Name": "收集", "Kind": "Input", "Outputs": [{ "Name": "背景", "Kind": "Context" }] } ] }
        ], "Flows": [] }
        """;

    /// <summary>库容器输出端口绑定成员的拆分支票端口，容器不透出拆分支票，装配校验拒绝。</summary>
    private const string LibraryContainerOutItemsPort = """
        { "Nodes": [
          { "Name": "交付", "Kind": "Container", "Outputs": [{ "Name": "结果", "Ref": "计划@拆分" }], "Nodes": [ { "Name": "计划", "Kind": "Executable", "Outputs": [{ "Name": "拆分", "Kind": "Items" }] } ] }
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
                { "Name": "撰写组", "Kind": "Container", "Nodes": [ { "Name": "实施", "Kind": "Executable", "Outputs": [{ "Name": "结论", "Kind": "Data" }] } ] }
              ]
            }
          ], "Flows": [] }
        """;

    /// <summary>执行节点声明容器输出端口，装配校验拒绝。</summary>
    private const string LeafWithOut = """
        { "Nodes": [
          { "Name": "交付", "Kind": "Container", "Nodes": [ { "Name": "实施", "Kind": "Executable", "Outputs": [{ "Name": "结论", "Ref": "实施" }] } ] }
        ], "Flows": [] }
        """;

    /// <summary>引用节点声明容器输出端口，装配校验拒绝。</summary>
    private const string ReferenceWithOut = """
        { "Nodes": [
          { "Name": "执行", "Kind": "Executable" },
          { "Name": "交付", "Kind": "Container", "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "执行者", "Outputs": [{ "Name": "结论", "Ref": "实施" }] } ] }
        ], "Flows": [] }
        """;

    /// <summary>装配层内联容器声明输出端口，绑定目标端口必须被成员声明。</summary>
    private const string AssemblyContainerOut = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Kind": "Container", "Outputs": [{ "Name": "结论", "Ref": "实施@不存在" }], "Nodes": [ { "Name": "实施", "Kind": "Executable", "Model": "执行者" } ] }
        ] } ] }
        """;

    /// <summary>校验节点库，取错误说明。</summary>
    private static IReadOnlyList<string> LibraryErrors(params NodeSpec[] library)
    {
        NodeLibrary nodeLibrary = new();
        foreach (NodeSpec node in library)
        {
            nodeLibrary.Register(node);
        }

        return [.. NodeLibraryRules.Validate(nodeLibrary).ErrorsOrEmptyList.Select(error => error.Description)];
    }
}

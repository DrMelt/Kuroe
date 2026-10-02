using ErrorOr;
using Kuroe.Configuration;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Tests;

/// <summary>节点库与装配引用：定义复用、端口绑定、实例命名与保存往返。</summary>
public sealed class NodeLibraryTests
{
    [Fact]
    public void Flow_references_library_leaf_and_container_with_port_binding()
    {
        using KuroeHarness harness = KuroeHarness.Create(LibraryFlow);

        FlowDefinition flow = harness.Flows.Find("默认").ThrowIfError();

        // 展开后：根容器包住制定计划与交付容器，容器成员名带实例前缀，@计划 绑定为制定计划
        NodeSpec root = flow.RootNode;
        Assert.NotNull(root.Nodes);
        Assert.Equal(2, root.Nodes.Count);
        NodeSpec plan = root.Nodes[0];
        Assert.NotNull(plan.Execution);
        Assert.Equal(NodeOutput.Plan, plan.Execution.Output);
        Assert.Empty(plan.From);

        NodeSpec deliver = root.Nodes[1];
        Assert.NotNull(deliver.Nodes);
        NodeSpec implement = deliver.Nodes[0];
        Assert.Equal(new NodeName("交付.实施"), implement.Name);
        Assert.NotNull(implement.Execution);
        Assert.Equal(NodeMode.PerItem, implement.Execution.Mode);
        Assert.Equal([new NodeName("制定计划")], implement.From);
    }

    [Fact]
    public void Same_library_container_instantiated_twice_keeps_members_distinct()
    {
        using KuroeHarness harness = KuroeHarness.Create(DualInstanceFlow);

        TaskSnapshot done = harness.Settle(harness.Submit("补齐 README", "双线"));

        Assert.Equal(TaskState.Done, done.State);
    }

    [Fact]
    public void Container_model_slot_bound_to_distinct_models_per_flow()
    {
        using KuroeHarness harness = KuroeHarness.Create(PerFlowModelBinding);

        FlowDefinition first = harness.Flows.Find("甲").ThrowIfError();
        NodeSpec firstImplement = first.RootNode.Nodes![1].Nodes![0];
        Assert.Equal(new ModelRef("执行者"), firstImplement.Model!.Value);

        FlowDefinition second = harness.Flows.Find("乙").ThrowIfError();
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

    [Fact]
    public void Import_rejects_duplicated_flow_names()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        File.WriteAllText(Path.Combine(harness.Root, "duplicated.json"), DuplicateImportedFlows);

        ErrorOr<FlowImport> imported = harness.Flows.Import("duplicated.json");

        Assert.True(imported.IsError);
        Assert.Contains(imported.ErrorsOrEmptyList, error => error.Description.Contains("流程名重复"));
    }

    [Fact]
    public void Port_binding_value_resolves_outer_member_name()
    {
        using KuroeHarness harness = KuroeHarness.Create(MemberNameBindingFlow);

        FlowDefinition flow = harness.Flows.Find("默认").ThrowIfError();

        NodeSpec deliver = flow.RootNode.Nodes![1];
        NodeSpec implement = deliver.Nodes![1].Nodes![0];
        Assert.Equal(new NodeName("交付.里.实施"), implement.Name);
        Assert.Equal([new NodeName("交付.目标条目")], implement.From);
    }

    [Fact]
    public void Port_binding_value_passes_through_outer_scope()
    {
        using KuroeHarness harness = KuroeHarness.Create(OuterPortBindingFlow);

        FlowDefinition flow = harness.Flows.Find("透传").ThrowIfError();

        NodeSpec deliver = flow.RootNode.Nodes![1];
        NodeSpec implement = deliver.Nodes![0].Nodes![0];
        Assert.Equal([new NodeName("制定计划")], implement.From);
    }

    [Fact]
    public void Reference_executable_inherits_library_validate_and_anyof()
    {
        using KuroeHarness harness = KuroeHarness.Create(ReferenceInheritFlow);

        FlowDefinition flow = harness.Flows.Find("默认").ThrowIfError();
        NodeSpec reviewed = flow.RootNode.Nodes![1];
        Assert.Equal(ValidationPredicate.TextContains, reviewed.Execution!.Validate!.Predicate);
        Assert.Equal(new NodeName("计划"), reviewed.Execution.AnyOf.Single()[0]);
    }

    [Fact]
    public void Reference_validate_overrides_library_definition()
    {
        using KuroeHarness harness = KuroeHarness.Create(ReferenceOverrideValidateFlow);

        FlowDefinition flow = harness.Flows.Find("默认").ThrowIfError();
        NodeSpec reviewed = flow.RootNode.Nodes![1];
        Assert.Equal(ValidationPredicate.NonEmpty, reviewed.Execution!.Validate!.Predicate);
    }

    [Fact]
    public void Reference_executable_with_inherited_validate_blocks_bad_output()
    {
        using KuroeHarness harness = KuroeHarness.Create(ReferenceInheritFlow);
        harness.Executor.Output = run => run.Context.NodeIndex switch
        {
            1 => "计划产出",
            _ => "修订产出不达标",
        };

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(NodeState.Blocked, Assert.Single(blocked.ExecutableStates, state => state.Index == 2).State);
    }

    [Fact]
    public void Reference_executable_with_inherited_anyof_releases_done()
    {
        using KuroeHarness harness = KuroeHarness.Create(ReferenceInheritFlow);
        harness.Executor.Output = run => run.Context.NodeIndex switch
        {
            1 => "计划产出",
            _ => "修订产出已通过",
        };

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables[1].Runs);
    }

    [Fact]
    public void Reference_inherited_anyof_conflicts_with_injected_from()
    {
        ErrorOr<KuroeHarness> harness = KuroeHarness.TryCreate(ReferenceAnyOfFromConflictFlow);

        Assert.True(harness.IsError);
        Assert.Contains(harness.ErrorsOrEmptyList, error => error.Description.Contains("不能同时出现在 From 与 AnyOf"));
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
    [InlineData(LibraryLeafIn, "执行节点不能声明输入端口绑定")]
    [InlineData(ContainerWithFrom, "不支持 From")]
    [InlineData(ReferenceWithInputs, "引用节点不能声明输入端口")]
    [InlineData(LeafWithInputs, "执行节点不能声明输入端口")]
    [InlineData(ContainerWithExecutionFields, "不支持执行配置")]
    [InlineData(LibraryLeafWithModel, "库执行定义不声明模型")]
    [InlineData(LibraryMemberWithoutSlot, "必须声明模型槽位")]
    [InlineData(MissingModelBinding, "没有外部绑定")]
    [InlineData(ContainerReferenceWithModel, "引用节点组不能声明模型")]
    [InlineData(LeafReferenceWithModelBindings, "引用执行节点不能声明模型绑定")]
    [InlineData(GroupReferenceWithAnyOf, "引用节点组不能声明 AnyOf")]
    [InlineData(GroupReferenceWithValidate, "引用节点组不能声明 Validate")]
    [InlineData(MemberAnyOfOutsideScope, "AnyOf 引用的节点")]
    [InlineData(MemberAnyOfUndeclaredPort, "没有在此容器上声明")]
    public void Invalid_library_fails_loading(string flowsJson, string expected)
    {
        ErrorOr<KuroeHarness> harness = KuroeHarness.TryCreate(flowsJson);

        Assert.True(harness.IsError);
        Assert.Contains(harness.ErrorsOrEmptyList, error => error.Description.Contains(expected));
    }

    private const string LibraryFlow = """
        {
          "Nodes": [
            { "Name": "执行", "Mode": "PerItem" },
            {
              "Name": "交付",
              "Inputs": ["计划"],
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
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
                { "Name": "交付", "Use": "交付", "In": { "计划": "制定计划" }, "Models": { "执行者": "执行者" } }
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
            { "Name": "规划", "Output": "Plan" },
            { "Name": "执行", "Mode": "PerItem" },
            { "Name": "交付", "Inputs": ["计划"], "Nodes": [
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
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                { "Name": "制定A", "Use": "规划", "Model": "规划者" },
                { "Name": "制定B", "Use": "规划", "Model": "规划者" },
                { "Name": "交付A", "Use": "交付", "In": { "计划": "制定A" }, "Models": { "执行者": "执行者" } },
                { "Name": "交付B", "Use": "交付", "In": { "计划": "制定B" }, "Models": { "执行者": "执行者" } }
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
            { "Name": "执行", "Mode": "PerItem" },
            { "Name": "交付", "Inputs": ["计划"], "Nodes": [
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
              "Nodes": [ { "Name": "整体", "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
                { "Name": "交付", "Use": "交付", "In": { "计划": "制定计划" }, "Models": { "执行者": "执行者" } }
              ] } ]
            },
            {
              "Name": "乙",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "实施者", "Model": "fake" }
              ],
              "Nodes": [ { "Name": "整体", "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
                { "Name": "交付", "Use": "交付", "In": { "计划": "制定计划" }, "Models": { "执行者": "实施者" } }
              ] } ]
            }
          ]
        }
        """;

    private const string LibraryDuplicateName = """
        { "Nodes": [
          { "Name": "执行", "Mode": "PerItem" },
          { "Name": "执行", "Mode": "PerItem" }
        ], "Flows": [] }
        """;

    private const string LibraryReferenceAtRoot = """
        { "Nodes": [ { "Name": "执行", "Mode": "PerItem" }, { "Name": "副本", "Use": "执行" } ], "Flows": [] }
        """;

    private const string LibraryLeafFrom = """
        { "Nodes": [ { "Name": "执行", "Mode": "PerItem", "From": ["制定计划"] } ], "Flows": [] }
        """;

    private const string UnknownLibraryReference = """
        { "Nodes": [
          { "Name": "执行", "Mode": "PerItem" },
          { "Name": "交付", "Nodes": [ { "Name": "实施", "Use": "不存在", "Model": "执行者" } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "Models": { "执行者": "执行者" } } ] }
        ] }
        """;

    private const string UnknownReferenceWithoutModel = """
        { "Nodes": [ { "Name": "执行", "Mode": "PerItem" } ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Nodes": [ { "Name": "实施", "Use": "不存在" } ] } ] }
        ] }
        """;

    private const string UnboundPort = """
        { "Nodes": [
          { "Name": "执行", "Mode": "PerItem" },
          { "Name": "交付", "Inputs": ["计划"], "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "执行者", "From": ["@计划"] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "Models": { "执行者": "执行者" } } ] }
        ] }
        """;

    private const string PortNotDeclared = """
        { "Nodes": [
          { "Name": "执行", "Mode": "PerItem" },
          { "Name": "交付", "Inputs": ["计划"], "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "执行者", "From": ["@来源"] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "Models": { "执行者": "执行者" } } ] }
        ] }
        """;

    private const string ReferenceWithBody = """
        { "Nodes": [
          { "Name": "执行", "Mode": "PerItem" },
          { "Name": "交付", "Inputs": ["计划"], "Nodes": [ { "Name": "实施", "Use": "执行", "From": ["@计划"], "Mode": "PerItem" } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "In": { "计划": "制定计划" }, "Models": { "执行者": "执行者" } } ] }
        ] }
        """;

    private const string UnknownPortBinding = """
        { "Nodes": [
          { "Name": "执行", "Mode": "PerItem" },
          { "Name": "交付", "Inputs": ["计划"], "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "执行者", "From": ["@计划"] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "In": { "来源": "制定计划" }, "Models": { "执行者": "执行者" } } ] }
        ] }
        """;

    private const string LibraryLeafIn = """
        { "Nodes": [ { "Name": "执行", "Mode": "PerItem", "In": { "x": "y" } } ], "Flows": [] }
        """;

    private const string ContainerWithFrom = """
        { "Nodes": [ { "Name": "组", "From": ["x"], "Nodes": [ { "Name": "实施", "Model": "执行者" } ] } ], "Flows": [] }
        """;

    private const string ReferenceWithInputs = """
        { "Nodes": [ { "Name": "执行", "Mode": "PerItem" } ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [ { "Name": "整体", "Nodes": [ { "Name": "实施", "Use": "执行", "Inputs": ["计划"] } ] } ] }
        ] }
        """;

    private const string LeafWithInputs = """
        { "Nodes": [], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [ { "Name": "整体", "Nodes": [ { "Name": "实施", "Model": "执行者", "Inputs": ["计划"] } ] } ] }
        ] }
        """;

    private const string ContainerWithExecutionFields = """
        { "Nodes": [ { "Name": "执行", "Mode": "PerItem" } ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [ { "Name": "组", "Model": "执行者", "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "执行者" } ] } ] }
        ] }
        """;

    private const string ReferenceWithGate = """
        { "Nodes": [ { "Name": "执行", "Mode": "PerItem" } ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [ { "Name": "整体", "Nodes": [ { "Name": "实施", "Use": "执行", "Gate": "Review" } ] } ] }
        ] }
        """;

    private const string ReferenceContainerFrom = """
        { "Nodes": [
          { "Name": "执行", "Mode": "PerItem" },
          { "Name": "交付", "Inputs": ["计划"], "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "执行者", "From": ["@计划"] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "Models": { "执行者": "执行者" }, "From": ["制定计划"] } ] }
        ] }
        """;

    private const string AssemblyContainerInputs = """
        { "Nodes": [], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "组", "Inputs": ["计划"], "Nodes": [ { "Name": "实施", "Model": "执行者" } ] } ] }
        ] }
        """;

    private const string DuplicateMemberNames = """
        { "Nodes": [
          { "Name": "执行", "Mode": "PerItem" },
          { "Name": "组", "Nodes": [
            { "Name": "实施", "Model": "执行者" },
            { "Name": "内", "Nodes": [ { "Name": "实施", "Model": "执行者" } ] }
          ] }
        ], "Flows": [] }
        """;

    private const string LibraryLeafWithModel = """
        { "Nodes": [ { "Name": "执行", "Model": "执行者", "Mode": "PerItem" } ], "Flows": [] }
        """;

    private const string LibraryMemberWithoutSlot = """
        { "Nodes": [
          { "Name": "执行", "Mode": "PerItem" },
          { "Name": "交付", "Inputs": ["计划"], "Nodes": [ { "Name": "实施", "Use": "执行", "From": ["@计划"] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "Models": { "执行者": "执行者" } } ] }
        ] }
        """;

    private const string MissingModelBinding = """
        { "Nodes": [
          { "Name": "执行", "Mode": "PerItem" },
          { "Name": "交付", "Inputs": ["计划"], "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "执行者", "From": ["@计划"] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付" } ] }
        ] }
        """;

    private const string ContainerReferenceWithModel = """
        { "Nodes": [
          { "Name": "执行", "Mode": "PerItem" },
          { "Name": "交付", "Inputs": ["计划"], "Nodes": [ { "Name": "实施", "Use": "执行", "Model": "执行者", "From": ["@计划"] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "Model": "执行者" } ] }
        ] }
        """;

    private const string LeafReferenceWithModelBindings = """
        { "Nodes": [ { "Name": "执行", "Mode": "PerItem" } ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [ { "Name": "整体", "Nodes": [ { "Name": "实施", "Use": "执行", "Models": { "执行者": "执行者" } } ] } ] }
        ] }
        """;

    private const string OuterPortBindingFlow = """
        {
          "Nodes": [
            { "Name": "执行", "Mode": "PerItem" },
            { "Name": "内层", "Inputs": ["内部"], "Nodes": [
              { "Name": "实施", "Use": "执行", "Model": "执行者", "From": ["@内部"] }
            ] },
            { "Name": "外层", "Inputs": ["目标"], "Nodes": [
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
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan", "Prompt": "把目标拆成可独立实施的条目。" },
                { "Name": "交付", "Use": "外层", "In": { "目标": "制定计划" }, "Models": { "执行者": "执行者" } }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private const string DuplicateImportedFlows = """
        { "Flows": [
          { "Name": "重复", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [ { "Name": "实施", "Model": "执行者" } ] },
          { "Name": "重复", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [ { "Name": "实施", "Model": "执行者" } ] }
        ] }
        """;

    private const string MemberNameBindingFlow = """
        {
          "Nodes": [
            { "Name": "执行", "Mode": "Single" },
            { "Name": "内层", "Inputs": ["内部"], "Nodes": [
              { "Name": "实施", "Use": "执行", "Model": "执行者", "From": ["@内部"] }
            ] },
            { "Name": "外层", "Inputs": ["目标"], "Nodes": [
              { "Name": "目标条目", "Model": "执行者" },
              { "Name": "里", "Use": "内层", "In": { "内部": "目标条目" }, "Models": { "执行者": "执行者" } }
            ] }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" }
              ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                { "Name": "规划", "Model": "规划者", "Output": "Plan" },
                { "Name": "交付", "Use": "外层", "In": { "目标": "规划" }, "Models": { "执行者": "执行者" } }
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

        FlowDefinition flow = harness.Flows.Find("单线交付").ThrowIfError();
        NodeSpec root = flow.RootNode;
        Assert.NotNull(root.Nodes);
        Assert.Equal(2, root.Nodes.Count);
    }

    private const string DocumentExampleFlow = """
        {
          "Nodes": [
            {
              "Name": "允许工具",
              "Tools": ["GetLocalTime", "GetWeather"],
              "Mode": "PerItem"
            },
            {
              "Name": "交付",
              "Inputs": ["计划"],
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
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan", "Prompt": "把目标拆成可独立实施的条目。" },
                { "Name": "交付", "Use": "交付", "In": { "计划": "制定计划" }, "Models": { "执行者": "执行者" } }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>库执行定义带校验与 AnyOf 组，流程引用不覆盖时展开后继承。</summary>
    private const string ReferenceInheritFlow = """"
        {
          "Nodes": [
            {
              "Name": "审查",
              "Mode": "Single",
              "Validate": { "Predicate": "TextContains", "Argument": "通过" },
              "AnyOf": [["计划"]]
            }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" }
              ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                    { "Name": "计划", "Model": "规划者", "Output": "Plan" },
                    { "Name": "审查实例", "Use": "审查", "Model": "执行者" }
                  ]
                }
              ]
            }
          ]
        }
        """";

    /// <summary>引用侧显式声明校验时覆盖库定义。</summary>
    private const string ReferenceOverrideValidateFlow = """"
        {
          "Nodes": [
            {
              "Name": "审查",
              "Mode": "Single",
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
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                    { "Name": "计划", "Model": "规划者", "Output": "Plan" },
                    { "Name": "审查实例", "Use": "审查", "Model": "执行者",
                      "Validate": { "Predicate": "NonEmpty" } }
                  ]
                }
              ]
            }
          ]
        }
        """";

    /// <summary>库定义继承的 AnyOf 与引用侧注入的 From 引用同一来源，按非重叠约束拒绝。</summary>
    private const string ReferenceAnyOfFromConflictFlow = """"
        {
          "Nodes": [
            {
              "Name": "审查",
              "Mode": "Single",
              "AnyOf": [["计划"]]
            }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" }
              ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                    { "Name": "计划", "Model": "规划者" },
                    { "Name": "审查实例", "Use": "审查", "Model": "执行者", "From": ["计划"] }
                  ]
                }
              ]
            }
          ]
        }
        """";
    /// <summary>引用节点组不能带 AnyOf，起点条件组只属于执行节点。</summary>
    private const string GroupReferenceWithAnyOf = """"
        {
          "Nodes": [
            { "Name": "审查", "Mode": "Single" },
            {
              "Name": "组",
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
                  "Nodes": [
                    { "Name": "计划", "Model": "执行者" },
                    { "Name": "引用组", "Use": "组", "AnyOf": [["计划"]] }
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
            { "Name": "审查", "Mode": "Single" },
            {
              "Name": "组",
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
                  "Nodes": [
                    { "Name": "计划", "Model": "执行者" },
                    { "Name": "引用组", "Use": "组", "Validate": { "Predicate": "NonEmpty" } }
                  ]
                }
              ]
            }
          ]
        }
        """";

    /// <summary>库容器成员执行节点的 AnyOf 必须落在容器作用域内。</summary>
    private const string MemberAnyOfOutsideScope = """"
        {
          "Nodes": [
            {
              "Name": "交付",
              "Nodes": [
                { "Name": "甲", "Model": "执行者", "AnyOf": [["局外"]] }
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
                  "Nodes": [
                    { "Name": "交付实例", "Use": "交付", "Models": { "执行者": "执行者" } }
                  ]
                }
              ]
            }
          ]
        }
        """";

    /// <summary>库容器成员执行节点的 AnyOf 端口必须已由容器定义声明。</summary>
    private const string MemberAnyOfUndeclaredPort = """"
        {
          "Nodes": [
            {
              "Name": "交付",
              "Nodes": [
                { "Name": "甲", "Model": "执行者", "AnyOf": [["@计划"]] }
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
                  "Nodes": [
                    { "Name": "交付实例", "Use": "交付", "Models": { "执行者": "执行者" } }
                  ]
                }
              ]
            }
          ]
        }
        """";
}

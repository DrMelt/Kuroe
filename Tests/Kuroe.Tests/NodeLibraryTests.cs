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

        Workflow flow = harness.Flows.Find("默认").ThrowIfError();

        // 展开后：制定计划 + 交付容器，容器成员名带实例前缀，@计划 绑定为制定计划
        Assert.Equal(2, flow.Nodes.Count);
        NodeSpec plan = flow.Nodes[0];
        Assert.NotNull(plan.Execution);
        Assert.Equal(NodeOutput.Plan, plan.Execution.Output);
        Assert.Empty(plan.From);

        NodeSpec deliver = flow.Nodes[1];
        Assert.NotNull(deliver.Nodes);
        NodeSpec implement = deliver.Nodes[0];
        Assert.Equal(new NodeName("交付.实施"), implement.Name);
        Assert.NotNull(implement.Execution);
        Assert.Equal(NodeMode.PerItem, implement.Execution.Mode);
        Assert.Equal([new NodeName("制定计划")], implement.From);

        NodeSpec review = deliver.Nodes[1];
        Assert.Equal(new NodeName("交付.审查"), review.Name);
        Assert.NotNull(review.Execution);
        Assert.Equal(NodeOutput.Review, review.Execution.Output);
        Assert.Equal([new NodeName("制定计划"), new NodeName("交付.实施")], review.From);
    }

    [Fact]
    public void Same_library_container_instantiated_twice_keeps_members_distinct()
    {
        using KuroeHarness harness = KuroeHarness.Create(DualInstanceFlow);

        TaskSnapshot done = harness.Settle(harness.Submit("补齐 README", "双线"));

        Assert.Equal(TaskState.Done, done.State);
    }

    [Fact]
    public void Import_keeps_library_and_reference_shape_on_save()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        File.WriteAllText(Path.Combine(harness.Root, "library.json"), LibraryFlow);

        WorkflowImport imported = harness.Flows.Import("library.json").ThrowIfError();
        Assert.Contains(imported.Notes, note => note.Contains("节点库"));

        string text = File.ReadAllText(KuroePaths.At(harness.Root).WorkflowFile);
        Assert.Contains("\"Nodes\"", text);
        Assert.Contains("\"Use\"", text);
        Assert.Contains("\"In\"", text);
        Assert.Contains("\"Inputs\"", text);
    }

    [Fact]
    public void Import_rejects_duplicated_flow_names()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        File.WriteAllText(Path.Combine(harness.Root, "duplicated.json"), DuplicateImportedFlows);

        ErrorOr<WorkflowImport> imported = harness.Flows.Import("duplicated.json");

        Assert.True(imported.IsError);
        Assert.Contains(imported.ErrorsOrEmptyList, error => error.Description.Contains("流程名重复"));
    }

    [Fact]
    public void Port_binding_value_resolves_outer_member_name()
    {
        using KuroeHarness harness = KuroeHarness.Create(MemberNameBindingFlow);

        Workflow flow = harness.Flows.Find("默认").ThrowIfError();

        NodeSpec deliver = flow.Nodes[1];
        NodeSpec implement = deliver.Nodes![1].Nodes![0];
        Assert.Equal(new NodeName("交付.里.实施"), implement.Name);
        Assert.Equal([new NodeName("交付.目标条目")], implement.From);
    }

    [Fact]
    public void Port_binding_value_passes_through_outer_scope()
    {
        using KuroeHarness harness = KuroeHarness.Create(OuterPortBindingFlow);

        Workflow flow = harness.Flows.Find("透传").ThrowIfError();

        NodeSpec deliver = flow.Nodes[1];
        NodeSpec implement = deliver.Nodes![0].Nodes![0];
        Assert.Equal([new NodeName("制定计划")], implement.From);
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
    public void Invalid_library_fails_loading(string flowsJson, string expected)
    {
        ErrorOr<KuroeHarness> harness = KuroeHarness.TryCreate(flowsJson);

        Assert.True(harness.IsError);
        Assert.Contains(harness.ErrorsOrEmptyList, error => error.Description.Contains(expected));
    }

    private const string LibraryFlow = """
        {
          "Nodes": [
            { "Name": "执行", "Model": "执行者", "Mode": "PerItem" },
            {
              "Name": "交付并检查",
              "Inputs": ["计划"],
              "Nodes": [
                { "Name": "实施", "Use": "执行", "From": ["@计划"] },
                { "Name": "审查", "Model": "检查者", "Output": "Review", "From": ["@计划", "实施"] }
              ]
            }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" },
                { "Name": "检查者", "Model": "fake" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
                { "Name": "交付", "Use": "交付并检查", "In": { "计划": "制定计划" } }
              ]
            }
          ]
        }
        """;
    private const string DualInstanceFlow = """
        {
          "Nodes": [
            { "Name": "规划", "Model": "规划者", "Output": "Plan" },
            { "Name": "执行", "Model": "执行者", "Mode": "PerItem" },
            { "Name": "检查", "Model": "检查者", "Output": "Review" },
            { "Name": "交付", "Inputs": ["计划"], "Nodes": [
              { "Name": "实施", "Use": "执行", "From": ["@计划"] },
              { "Name": "审查", "Use": "检查", "From": ["@计划", "实施"] }
            ] }
          ],
          "Flows": [
            {
              "Name": "双线",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" },
                { "Name": "检查者", "Model": "fake" }
              ],
              "Nodes": [
                { "Name": "制定A", "Use": "规划" },
                { "Name": "制定B", "Use": "规划" },
                { "Name": "交付A", "Use": "交付", "In": { "计划": "制定A" } },
                { "Name": "交付B", "Use": "交付", "In": { "计划": "制定B" } }
              ]
            }
          ]
        }
        """;

    private const string LibraryDuplicateName = """
        { "Nodes": [
          { "Name": "执行", "Model": "执行者" },
          { "Name": "执行", "Model": "执行者" }
        ], "Flows": [] }
        """;

    private const string LibraryReferenceAtRoot = """
        { "Nodes": [ { "Name": "执行", "Model": "执行者" }, { "Name": "副本", "Use": "执行" } ], "Flows": [] }
        """;

    private const string LibraryLeafFrom = """
        { "Nodes": [ { "Name": "执行", "Model": "执行者", "From": ["制定计划"] } ], "Flows": [] }
        """;

    private const string UnknownLibraryReference = """
        { "Nodes": [
          { "Name": "执行", "Model": "执行者" },
          { "Name": "交付", "Nodes": [ { "Name": "实施", "Use": "不存在" } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付" } ] }
        ] }
        """;

    private const string UnboundPort = """
        { "Nodes": [
          { "Name": "执行", "Model": "执行者" },
          { "Name": "交付", "Inputs": ["计划"], "Nodes": [ { "Name": "实施", "Use": "执行", "From": ["@计划"] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付" } ] }
        ] }
        """;

    private const string PortNotDeclared = """
        { "Nodes": [
          { "Name": "执行", "Model": "执行者" },
          { "Name": "交付", "Inputs": ["计划"], "Nodes": [ { "Name": "实施", "Use": "执行", "From": ["@来源"] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付" } ] }
        ] }
        """;

    private const string ReferenceWithBody = """
        { "Nodes": [
          { "Name": "执行", "Model": "执行者" },
          { "Name": "交付", "Inputs": ["计划"], "Nodes": [ { "Name": "实施", "Use": "执行", "From": ["@计划"], "Model": "执行者" } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "In": { "计划": "制定计划" } } ] }
        ] }
        """;

    private const string UnknownPortBinding = """
        { "Nodes": [
          { "Name": "执行", "Model": "执行者" },
          { "Name": "交付", "Inputs": ["计划"], "Nodes": [ { "Name": "实施", "Use": "执行", "From": ["@计划"] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "In": { "来源": "制定计划" } } ] }
        ] }
        """;

    private const string LibraryLeafIn = """
        { "Nodes": [ { "Name": "执行", "Model": "执行者", "In": { "x": "y" } } ], "Flows": [] }
        """;

    private const string ContainerWithFrom = """
        { "Nodes": [ { "Name": "组", "From": ["x"], "Nodes": [ { "Name": "实施", "Model": "执行者" } ] } ], "Flows": [] }
        """;

    private const string ReferenceWithInputs = """
        { "Nodes": [ { "Name": "执行", "Model": "执行者" } ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [ { "Name": "实施", "Use": "执行", "Inputs": ["计划"] } ] }
        ] }
        """;

    private const string LeafWithInputs = """
        { "Nodes": [], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [ { "Name": "实施", "Model": "执行者", "Inputs": ["计划"] } ] }
        ] }
        """;

    private const string ContainerWithExecutionFields = """
        { "Nodes": [ { "Name": "执行", "Model": "执行者" } ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [ { "Name": "组", "Model": "执行者", "Nodes": [ { "Name": "实施", "Use": "执行" } ] } ] }
        ] }
        """;

    private const string ReferenceWithGate = """
        { "Nodes": [ { "Name": "执行", "Model": "执行者" } ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [ { "Name": "实施", "Use": "执行", "Gate": "Review" } ] }
        ] }
        """;

    private const string ReferenceContainerFrom = """
        { "Nodes": [
          { "Name": "执行", "Model": "执行者" },
          { "Name": "交付", "Inputs": ["计划"], "Nodes": [ { "Name": "实施", "Use": "执行", "From": ["@计划"] } ] }
        ], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "交付", "Use": "交付", "From": ["制定计划"] } ] }
        ] }
        """;

    private const string AssemblyContainerInputs = """
        { "Nodes": [], "Flows": [
          { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [ { "Name": "组", "Inputs": ["计划"], "Nodes": [ { "Name": "实施", "Model": "执行者" } ] } ] }
        ] }
        """;

    private const string DuplicateMemberNames = """
        { "Nodes": [
          { "Name": "执行", "Model": "执行者" },
          { "Name": "组", "Nodes": [
            { "Name": "实施", "Model": "执行者" },
            { "Name": "内", "Nodes": [ { "Name": "实施", "Model": "执行者" } ] }
          ] }
        ], "Flows": [] }
        """;

    private const string OuterPortBindingFlow = """
        {
          "Nodes": [
            { "Name": "执行", "Model": "执行者", "Mode": "PerItem" },
            { "Name": "检查", "Model": "检查者", "Output": "Review", "OnReject": "Retry", "MaxAttempts": 2 },
            { "Name": "内层", "Inputs": ["内部"], "Nodes": [
              { "Name": "实施", "Use": "执行", "From": ["@内部"] }
            ] },
            { "Name": "外层", "Inputs": ["目标"], "Nodes": [
              { "Name": "里", "Use": "内层", "In": { "内部": "@目标" } },
              { "Name": "审查", "Use": "检查", "From": ["@目标", "里"] }
            ] }
          ],
          "Flows": [
            {
              "Name": "透传",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" },
                { "Name": "检查者", "Model": "fake" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan", "Prompt": "把目标拆成可独立实施的条目。" },
                { "Name": "交付", "Use": "外层", "In": { "目标": "制定计划" } }
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
            { "Name": "执行", "Model": "执行者" },
            { "Name": "检查", "Model": "检查者", "Output": "Review", "OnReject": "Retry", "MaxAttempts": 2 },
            { "Name": "内层", "Inputs": ["内部"], "Nodes": [
              { "Name": "实施", "Use": "执行", "From": ["@内部"] }
            ] },
            { "Name": "外层", "Inputs": ["目标"], "Nodes": [
              { "Name": "目标条目", "Model": "执行者" },
              { "Name": "里", "Use": "内层", "In": { "内部": "目标条目" } },
              { "Name": "审查", "Use": "检查", "From": ["@目标", "里"] }
            ] }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" },
                { "Name": "检查者", "Model": "fake" }
              ],
              "Nodes": [
                { "Name": "规划", "Model": "规划者", "Output": "Plan" },
                { "Name": "交付", "Use": "外层", "In": { "目标": "规划" } }
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

        Workflow flow = harness.Flows.Find("整体检查").ThrowIfError();
        Assert.Equal(2, flow.Nodes.Count);
    }

    private const string DocumentExampleFlow = """
        {
          "Nodes": [
            {
              "Name": "允许工具",
              "Model": "执行者",
              "Tools": ["GetLocalTime", "GetWeather"],
              "Mode": "PerItem"
            },
            {
              "Name": "检查",
              "Model": "检查者",
              "Output": "Review",
              "OnReject": "Retry",
              "MaxAttempts": 2
            },
            {
              "Name": "交付并检查",
              "Inputs": ["计划"],
              "Nodes": [
                { "Name": "实施", "Use": "允许工具", "From": ["@计划"] },
                { "Name": "审查", "Use": "检查", "From": ["@计划", "实施"] }
              ]
            }
          ],
          "Flows": [
            {
              "Name": "整体检查",
              "Description": "制定计划、复用交付并检查",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" },
                { "Name": "检查者", "Model": "fake" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan", "Prompt": "把目标拆成可独立实施的条目。" },
                { "Name": "交付", "Use": "交付并检查", "In": { "计划": "制定计划" } }
              ]
            }
          ]
        }
        """;
}
using Kuroe.Flow.Compilation;
using Kuroe.Flow.Graph.Shared.StaticGraph;
using ErrorOr;
using Kuroe.Flow.NodeType.Container;
using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.FlowDescription.Definition.Shared.Flow;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.FlowDescription.Definition.Shared.Ports;
using Kuroe.Tasks.Shared;
using Xunit;
using Kuroe.FlowDescription.Compilation.Library;

namespace Kuroe.FlowDescription.Compilation.Tests.Validation;

/// <summary>执行次数上限的解析：直接构造流程定义并编译，断言执行节点拿到上限，不启动任务。</summary>
public sealed class FlowRulesLimitTests
{
    private static readonly PortName StartPort = new("目标");

    [Fact(DisplayName = "未配置节点使用默认上限")]
    public void Unconfigured_node_uses_the_default_limit()
    {
        NodeGraph graph = Compile(Flow(Container("整体", max: null, source: null, Exec("干活"))));

        Assert.Equal(ExecutableSpec.DefaultMaxExecutions, Executable(graph, "干活").MaxExecutions);
    }

    [Fact(DisplayName = "显式成员上限保留")]
    public void Explicit_member_limit_is_kept()
    {
        NodeGraph graph = Compile(Flow(Container("整体", max: null, source: null, Exec("重点", max: 5), Exec("普通"))));

        Assert.Equal(5, Executable(graph, "重点").MaxExecutions);
    }

    [Fact(DisplayName = "容器上限保留在容器上")]
    public void Container_limit_is_kept_on_the_container()
    {
        NodeGraph graph = Compile(Flow(Container("整体", max: 2, source: null, Exec("普通"))));

        Assert.Equal(2, ContainerOf(graph, "整体").MaxExecutions);
        Assert.Equal(2, Assert.IsType<NodeInputPlan>(graph.PlanOf(NodePath.Of("整体"))).MaxExecutions);
        Assert.Equal(ExecutableSpec.DefaultMaxExecutions, Executable(graph, "普通").MaxExecutions);
    }

    [Fact(DisplayName = "成员上限独立于容器")]
    public void Member_limit_is_independent_of_the_container()
    {
        NodeGraph graph = Compile(Flow(Container("整体", max: 2, source: null, Exec("重点", max: 5), Exec("普通"))));

        Assert.Equal(5, Executable(graph, "重点").MaxExecutions);
        Assert.Equal(ExecutableSpec.DefaultMaxExecutions, Executable(graph, "普通").MaxExecutions);
    }

    [Fact(DisplayName = "嵌套容器上限保留")]
    public void Nested_container_limits_are_kept()
    {
        NodeGraph graph = Compile(Flow(Container("整体", max: 2, source: null,
            Exec("外层干活"),
            Container("内部", max: 5, source: PortRef.Bound(StartPort), Exec("内层干活")))));

        Assert.Equal(5, ContainerOf(graph, "内部").MaxExecutions);
        Assert.Equal(2, ContainerOf(graph, "整体").MaxExecutions);
        Assert.Equal(2, Assert.IsType<NodeInputPlan>(graph.PlanOf(NodePath.Of("整体"))).MaxExecutions);
        Assert.Equal(ExecutableSpec.DefaultMaxExecutions, Executable(graph, "内层干活").MaxExecutions);
        Assert.Equal(ExecutableSpec.DefaultMaxExecutions, Executable(graph, "外层干活").MaxExecutions);
    }

    private static ContainerNode ContainerOf(NodeGraph graph, string label) =>
        graph.Containers.Single(node => node.Label.Value == label);

    private static ExecutableNode Executable(NodeGraph graph, string label) =>
        graph.ExecutableNodes.Single(node => node.Label.Value == label);

    private static NodeGraph Compile(FlowDefinition flow)
    {
        ErrorOr<IReadOnlyList<FlowDefinition>> expanded = new NodeLibraryExpander().Expand([flow], [], []);
        Assert.False(expanded.IsError, string.Join("；", expanded.ErrorsOrEmptyList.Select(error => error.Description)));

        ErrorOr<NodeGraph> compiled = FlowCompiler.Compile(Assert.Single(expanded.Value));
        Assert.False(compiled.IsError, string.Join("；", compiled.ErrorsOrEmptyList.Select(error => error.Description)));

        return compiled.Value;
    }

    private static FlowDefinition Flow(NodeSpec root) =>
        new(new FlowName("默认"), null, [new Port(StartPort, PortTypes.Text)], root);

    private static NodeSpec Container(
        string name,
        int? max = null,
        PortRef? source = null,
        params NodeSpec[] members) =>
        NodeSpec.Define(new NodeName(name)) with
        {
            Type = ContainerNodeTypes.Type,
            MaxExecutions = max,
            Inputs = [new InputPort(StartPort)],
            From =
            [
                new FromGroup(
                [
                    new FromLink(StartPort, source ?? PortRef.Start(StartPort)),
                ]),
            ],
            Nodes = [.. members],
        };

    private static NodeSpec Exec(string name, int? max = null) =>
        NodeSpec.Define(new NodeName(name)) with
        {
            Type = TestNodeTypes.PlainExecutable,
            Spec = new ExecutableSpec { MaxExecutions = max },
            From = [StartRef()],
        };

    private static FromGroup StartRef() =>
        new([new FromLink(StartPort, PortRef.Bound(StartPort))]);
}

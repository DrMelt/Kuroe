using ErrorOr;
using Kuroe.Flow.Compilation.Graph;
using Kuroe.Flow.Compilation.Validation;
using Kuroe.Flow.NodeLibrary;
using Kuroe.Flow.NodeType.Container;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Kuroe.Flow.TestSupport;
using Xunit;

namespace Kuroe.Flow.Compilation.Tests;

/// <summary>执行次数上限的解析：直接构造流程定义并编译，断言执行节点拿到上限，不启动任务。</summary>
public sealed class LimitRuleTests
{
    private static readonly PortName StartPort = new("目标");

    [Fact]
    public void Unconfigured_node_uses_the_default_limit()
    {
        NodeGraph graph = Compile(Flow(Container("整体", max: null, source: null, Exec("干活"))));

        Assert.Equal(ExecutableNode.DefaultMaxExecutions, Executable(graph, "干活").MaxExecutions);
    }

    [Fact]
    public void Explicit_member_limit_is_kept()
    {
        NodeGraph graph = Compile(Flow(Container("整体", max: null, source: null, Exec("重点", max: 5), Exec("普通"))));

        Assert.Equal(5, Executable(graph, "重点").MaxExecutions);
    }

    [Fact]
    public void Container_limit_applies_to_unconfigured_members()
    {
        NodeGraph graph = Compile(Flow(Container("整体", max: 2, source: null, Exec("普通"))));

        Assert.Equal(2, Executable(graph, "普通").MaxExecutions);
    }

    [Fact]
    public void Explicit_member_limit_overrides_the_container()
    {
        NodeGraph graph = Compile(Flow(Container("整体", max: 2, source: null, Exec("重点", max: 5), Exec("普通"))));

        Assert.Equal(5, Executable(graph, "重点").MaxExecutions);
        Assert.Equal(2, Executable(graph, "普通").MaxExecutions);
    }

    [Fact]
    public void Nested_container_limit_overrides_the_outer_one()
    {
        NodeGraph graph = Compile(Flow(Container("整体", max: 2, source: null,
            Exec("外层干活"),
            Container("内部", max: 5, source: PortRef.Bound(StartPort), Exec("内层干活")))));

        Assert.Equal(5, Executable(graph, "内层干活").MaxExecutions);
        Assert.Equal(2, Executable(graph, "外层干活").MaxExecutions);
    }

    private static ExecutableNode Executable(NodeGraph graph, string label) =>
        graph.ExecutableNodes.Single(node => node.Label.Value == label);

    private static NodeGraph Compile(FlowDefinition flow)
    {
        ErrorOr<IReadOnlyList<FlowDefinition>> expanded = new NodeLibraryExpander().Expand([flow], [], []);
        Assert.False(expanded.IsError, string.Join("；", expanded.ErrorsOrEmptyList.Select(error => error.Description)));

        ErrorOr<NodeGraph> compiled = FlowRules.ValidateAndCompile(Assert.Single(expanded.Value));
        Assert.False(compiled.IsError, string.Join("；", compiled.ErrorsOrEmptyList.Select(error => error.Description)));

        return compiled.Value;
    }

    private static FlowDefinition Flow(NodeSpec root) =>
        new(new FlowName("默认"), null, [new Port(StartPort, PortTypes.Text)], new Dictionary<PortName, string>(), root);

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
            In = new Dictionary<PortName, PortRef>
            {
                [StartPort] = source ?? PortRef.Named(StartNode.ReservedLabel, StartPort),
            },
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

using Kuroe.Flows.Shared.StaticGraph;
using Kuroe.Flows.StaticGraph;
using ExecutableNode = Kuroe.Flows.Shared.StaticGraph.ExecutableNode;
using Xunit;

namespace Kuroe.Flows.Tests;

/// <summary>流程模型：执行配置的缺省设置与编译视图的按名定位。</summary>
public sealed class FlowModelTests
{
    [Fact]
    public void Executable_defaults_to_single_auto()
    {
        var node = new NodeSpec
        {
            Name = new NodeName("实施"),
            Kind = NodeKind.Executable,
            Model = new ModelRef("执行者"),
            Execution = new ExecutableSpec(),
        };

        ExecutableSpec executable = node.Execution;
        Assert.Equal(NodeMode.Single, executable.Mode);
        Assert.Equal(NodeGate.Auto, node.Gate);
        Assert.Null(executable.Question);
        Assert.Empty(node.From);
        Assert.Empty(executable.Tools);
    }

    [Fact]
    public void NodeGraph_locates_executables_by_name()
    {
        var graph = new NodeGraph(
        [
            new StartNode
            {
                Index = 0,
                Name = StartNode.ReservedName,
                Path = new NodePath([StartNode.ReservedName]),
                Gate = NodeGate.Auto,
                Outputs = [new Port(new PortName("目标"), PortType.Text)],
            },
            new ExecutableNode
            {
                Index = 1,
                Name = new NodeName("规划"),
                Path = new NodePath([new NodeName("规划")]),
                Gate = NodeGate.Auto,
                Kind = NodeKind.Executable,
                Execution = new ExecutableSpec { Mode = NodeMode.Single },
                Model = Planner,
                Outputs = [],
                SystemPrompt = [],
                MaxRuns = 100,
            },
            new ExecutableNode
            {
                Index = 2,
                Name = new NodeName("实施"),
                Path = new NodePath([new NodeName("实施")]),
                Gate = NodeGate.Auto,
                Kind = NodeKind.Executable,
                Execution = new ExecutableSpec { Mode = NodeMode.Single },
                Model = Planner,
                Outputs = [],
                SystemPrompt = [],
                MaxRuns = 100,
            },
        ], []);

        Assert.Equal(3, graph.Count);
        Assert.Equal(2, graph.TotalExecutables);
        Assert.Equal(1, graph.IndexOf(new NodeName("规划")));
        Assert.Equal(2, graph.IndexOf(new NodeName("实施")));
        Assert.Null(graph.IndexOf(new NodeName("检查")));
    }

    [Fact]
    public void FlowName_create_trims_and_rejects_blank()
    {
        Assert.Equal(new FlowName("默认"), FlowName.Create(" 默认 ").Value);
        Assert.True(FlowName.Create(string.Empty).IsError);
        Assert.True(FlowName.Create("   ").IsError);
    }

    [Fact]
    public void PortName_create_trims_and_rejects_at_or_blank()
    {
        Assert.Equal(new PortName("计划"), PortName.Create("计划").Value);
        Assert.True(PortName.Create("来源@端口").IsError);
        Assert.True(PortName.Create(string.Empty).IsError);
    }

    private static readonly ModelDefinition Planner = new() { Name = new ModelRef("执行者") };
}

using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Graph;
using ExecutableNode = Kuroe.Shared.Workflows.Graph.ExecutableNode;
using Xunit;

namespace Kuroe.Shared.Tests;

/// <summary>流程模型：执行配置的缺省设置与编译视图的按名定位。</summary>
public sealed class WorkflowModelTests
{
    [Fact]
    public void Executable_defaults_to_plain_single_auto()
    {
        var node = new NodeSpec { Name = new NodeName("实施"), Model = new ModelRef("执行者"), Execution = new ExecutableSpec() };

        ExecutableSpec executable = node.Execution;
        Assert.Equal(NodeOutput.Plain, executable.Output);
        Assert.Equal(NodeMode.Single, executable.Mode);
        Assert.Equal(NodeGate.Auto, node.Gate);
        Assert.Empty(node.From);
        Assert.Empty(executable.Tools);
    }

    [Fact]
    public void NodeGraph_locates_executables_by_name()
    {
        var graph = new NodeGraph(
        [
            new ExecutableNode(0, new NodeName("规划"), "规划", NodeGate.Auto, Planner, [], null, NodeOutput.Plan, NodeMode.Single, null, [], [], null, null),
            new ExecutableNode(1, new NodeName("实施"), "实施", NodeGate.Auto, Planner, [], null, NodeOutput.Plain, NodeMode.Single, null, [0], [], null, null),
        ], []);

        Assert.Equal(2, graph.Count);
        Assert.Equal(0, graph.IndexOf(new NodeName("规划")));
        Assert.Equal(1, graph.IndexOf(new NodeName("实施")));
        Assert.Null(graph.IndexOf(new NodeName("检查")));
    }

    [Fact]
    public void ToolFunction_exposes_its_declaration()
    {
        ToolFunction function = new(new ToolName("GetTime"), "取时间", [], _ => "现在");

        Assert.Equal(new ToolName("GetTime"), function.Name);
        Assert.Equal("取时间", function.Description);
        Assert.Empty(function.Parameters);
        Assert.Equal("现在", function.Invoke(new ToolArguments(new Dictionary<string, object?>())));
    }

    private static readonly ModelDefinition Planner = new() { Name = new ModelRef("执行者") };
}
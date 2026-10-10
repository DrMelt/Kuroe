using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Graph;
using ExecutableNode = Kuroe.Shared.Workflows.Graph.ExecutableNode;
using Xunit;

namespace Kuroe.Shared.Tests;

/// <summary>流程模型：执行配置的缺省设置与编译视图的按名定位。</summary>
public sealed class FlowModelTests
{
    [Fact]
    public void Executable_defaults_to_text_single_auto()
    {
        var node = new NodeSpec { Name = new NodeName("实施"), Model = new ModelRef("执行者"), Execution = new ExecutableSpec() };

        ExecutableSpec executable = node.Execution;
        Assert.Equal(NodeOutput.Text, executable.Output);
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
            new ExecutableNode
            {
                Index = 0,
                Name = new NodeName("规划"),
                Path = new NodePath([new NodeName("规划")]),
                Gate = NodeGate.Auto,
                Execution = new ExecutableSpec { Output = NodeOutput.Plan, Mode = NodeMode.Single },
                Model = Planner,
                From = [],
                Outputs = [],
                SystemPrompt = [],
                MaxRuns = 100,
            },
            new ExecutableNode
            {
                Index = 1,
                Name = new NodeName("实施"),
                Path = new NodePath([new NodeName("实施")]),
                Gate = NodeGate.Auto,
                Execution = new ExecutableSpec { Output = NodeOutput.Text, Mode = NodeMode.Single },
                Model = Planner,
                From = [new Dependency(0, PortNames.Split)],
                Outputs = [],
                SystemPrompt = [],
                MaxRuns = 100,
            },
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
        Assert.Equal("现在", function.Invoke(new ToolArguments(new Dictionary<string, object?>())).Value);
    }

    [Fact]
    public void ToolFunction_path_defaults_to_name()
    {
        ToolFunction function = new(new ToolName("GetTime"), "取时间", [], _ => "现在");

        Assert.Equal(new ToolPath("GetTime"), function.Path);
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

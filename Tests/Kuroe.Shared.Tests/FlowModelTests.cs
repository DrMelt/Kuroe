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
                Outputs = [new Port(new PortName("目标"), PortKind.Data)],
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
                From = [],
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
                From = [new Dependency(1, new PortName("拆分"))],
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
    public void ToolDefinition_exposes_its_declaration()
    {
        ToolDefinition definition = new(new ToolName("GetTime"), "取时间", []);
        ToolFunction function = definition.Bind(_ => "现在");

        Assert.Equal(new ToolName("GetTime"), function.Definition.Name);
        Assert.Equal("取时间", function.Definition.Description);
        Assert.Empty(function.Definition.Parameters);
        Assert.Equal("现在", function.Invoke(new ToolArguments(new Dictionary<string, object?>())).Value);
    }

    [Fact]
    public void ToolDefinition_path_is_built_from_name_and_group()
    {
        ToolDefinition plain = new(new ToolName("GetTime"), "取时间", []);
        ToolDefinition grouped = new(new ToolName("ReadFile"), "读文件", [], new ToolPath("files"));
        ToolDefinition blank = new(new ToolName("GetTime"), "取时间", [], new ToolPath(string.Empty));

        Assert.Equal(new ToolPath("GetTime"), plain.Path);
        Assert.Equal(new ToolPath("files/ReadFile"), grouped.Path);
        Assert.Equal(new ToolPath("GetTime"), blank.Path);
    }

    [Fact]
    public void ToolDefinition_path_follows_derived_values()
    {
        ToolDefinition grouped = new(new ToolName("ReadFile"), "读文件", [], new ToolPath("files"));

        ToolDefinition renamed = grouped with { Name = new ToolName("WriteFile") };
        ToolDefinition regrouped = grouped with { Group = new ToolPath("source") };

        Assert.Equal(new ToolPath("files/WriteFile"), renamed.Path);
        Assert.Equal(new ToolPath("source/ReadFile"), regrouped.Path);
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

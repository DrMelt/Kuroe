using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;
using Xunit;

namespace Kuroe.Shared.Tests;

/// <summary>流程模型：执行节点的缺省配置与编译视图的按名定位。</summary>
public sealed class WorkflowModelTests
{
    [Fact]
    public void Executable_defaults_to_plain_single_auto_without_rework()
    {
        ExecuteNode executable = new() { Name = "实施", Model = "执行者" };

        Assert.Equal(NodeOutput.Plain, executable.Output);
        Assert.Equal(NodeMode.Single, executable.Mode);
        Assert.Equal(NodeGate.Auto, executable.Gate);
        Assert.Empty(executable.From);
        Assert.Null(executable.OnReject);
        Assert.Null(executable.MaxAttempts);
        Assert.Empty(executable.Tools);
    }

    [Fact]
    public void Check_executable_defaults_to_retry_once_more()
    {
        ExecuteNode executable = new() { Name = "检查", Model = "检查者", Output = NodeOutput.Review };

        Assert.Equal(RejectAction.Retry, executable.RejectAction);
        Assert.Equal(2, executable.AttemptLimit);
    }

    [Fact]
    public void Declared_rework_overrides_defaults()
    {
        ExecuteNode executable = new()
        {
            Name = "检查",
            Model = "检查者",
            Output = NodeOutput.Review,
            OnReject = RejectAction.Stop,
            MaxAttempts = 5,
        };

        Assert.Equal(RejectAction.Stop, executable.RejectAction);
        Assert.Equal(5, executable.AttemptLimit);
    }

    [Fact]
    public void NodeGraph_locates_executables_by_name()
    {
        var graph = new NodeGraph(
        [
            new ExecutableNode(0, "规划", "规划", Planner, [], null, NodeOutput.Plan, NodeMode.Single, null, [], NodeGate.Auto, null, null, null),
            new ExecutableNode(1, "实施", "实施", Planner, [], null, NodeOutput.Plain, NodeMode.Single, null, [0], NodeGate.Auto, null, null, null),
        ], []);

        Assert.Equal(2, graph.Count);
        Assert.Equal(0, graph.IndexOf("规划"));
        Assert.Equal(1, graph.IndexOf("实施"));
        Assert.Null(graph.IndexOf("检查"));
    }

    [Fact]
    public void ToolFunction_exposes_its_declaration()
    {
        ToolFunction function = new("GetTime", "取时间", [], _ => "现在");

        Assert.Equal("GetTime", function.Name);
        Assert.Equal("取时间", function.Description);
        Assert.Empty(function.Parameters);
        Assert.Equal("现在", function.Invoke(new ToolArguments(new Dictionary<string, object?>())));
    }

    private static readonly ModelDefinition Planner = new() { Name = "执行者" };
}
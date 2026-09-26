using Kuroe.Shared.Agent;
using Kuroe.Shared.Agent.Tools;
using Kuroe.Shared.Workflows.Flows;
using Xunit;

namespace Kuroe.Shared.Tests;

/// <summary>流程模型：叶子的缺省配置与编译视图的按名定位。</summary>
public sealed class WorkflowModelTests
{
    [Fact]
    public void Leaf_defaults_to_plain_single_auto_without_rework()
    {
        AgentNode leaf = new() { Name = "实施", Agent = "执行者" };

        Assert.Equal(NodeOutput.Plain, leaf.Output);
        Assert.Equal(NodeMode.Single, leaf.Mode);
        Assert.Equal(NodeGate.Auto, leaf.Gate);
        Assert.Empty(leaf.From);
        Assert.Null(leaf.OnReject);
        Assert.Null(leaf.MaxAttempts);
    }

    [Fact]
    public void Check_leaf_defaults_to_retry_once_more()
    {
        AgentNode leaf = new() { Name = "检查", Agent = "检查者", Output = NodeOutput.Review };

        Assert.Equal(RejectAction.Retry, leaf.RejectAction);
        Assert.Equal(2, leaf.AttemptLimit);
    }

    [Fact]
    public void Declared_rework_overrides_defaults()
    {
        AgentNode leaf = new()
        {
            Name = "检查",
            Agent = "检查者",
            Output = NodeOutput.Review,
            OnReject = RejectAction.Stop,
            MaxAttempts = 5,
        };

        Assert.Equal(RejectAction.Stop, leaf.RejectAction);
        Assert.Equal(5, leaf.AttemptLimit);
    }

    [Fact]
    public void NodeGraph_locates_leaves_by_name()
    {
        var graph = new NodeGraph(
        [
            new LeafNode(0, "规划", "规划", Agent, null, NodeOutput.Plan, NodeMode.Single, [], NodeGate.Auto, null, null, null),
            new LeafNode(1, "实施", "实施", Agent, null, NodeOutput.Plain, NodeMode.Single, [0], NodeGate.Auto, null, null, null),
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

    private static readonly AgentDefinition Agent = new() { Name = "执行者" };
}
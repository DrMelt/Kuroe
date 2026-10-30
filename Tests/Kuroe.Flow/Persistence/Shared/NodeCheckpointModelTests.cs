using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.TestSupport;
using Xunit;

namespace Kuroe.Flow.Persistence.Shared.Tests;

/// <summary>节点检查点的数据形状：公共状态与各节点类型的专属状态。</summary>
public sealed class NodeCheckpointModelTests
{
    [Fact]
    public void Node_checkpoint_carries_common_state()
    {
        TaskCheckpoint checkpoint = TaskCheckpointFixture.Create();
        NodePath plan = NodePath.Of("整体", "计划");

        NodeCheckpoint node = checkpoint.Nodes.Single(candidate => candidate.Path == plan);

        Assert.Equal(2, node.Common.Revision);
        Assert.Equal(0, node.Common.LastInputGroup);
        Assert.False(node.Common.Blocked);
        Assert.False(node.Common.RerunRequested);
        Assert.Single(node.Common.Consumed);
    }

    [Fact]
    public void Executable_checkpoint_carries_published_execution()
    {
        TaskCheckpoint checkpoint = TaskCheckpointFixture.Create();
        NodePath plan = NodePath.Of("整体", "计划");

        ExecutableNodeCheckpoint node = Assert.IsType<ExecutableNodeCheckpoint>(
            checkpoint.Nodes.Single(candidate => candidate.Path == plan));

        Assert.Equal(1, node.Executions);
        Assert.NotNull(node.PublishedExecution);
        Assert.Equal("计划结果", node.PublishedPorts[new PortName("结论")].Text);
    }

    [Fact]
    public void Self_driven_checkpoint_carries_content_and_failure()
    {
        TaskCheckpoint checkpoint = TaskCheckpointFixture.Create();
        NodePath read = NodePath.Of("整体", "读取");

        SelfDrivenNodeCheckpoint node = Assert.IsType<SelfDrivenNodeCheckpoint>(
            checkpoint.Nodes.Single(candidate => candidate.Path == read));

        Assert.Null(node.Content);
        Assert.Equal("读取失败。", node.Failure);
        Assert.False(node.FailureTaken);
        Assert.False(node.Retry);
        Assert.True(node.Common.Blocked);
        Assert.Equal([read], node.Common.ReworkTargets);
    }

    [Fact]
    public void Input_checkpoint_carries_answer_and_waiting()
    {
        TaskCheckpoint checkpoint = TaskCheckpointFixture.Create();
        NodePath answer = NodePath.Of("整体", "回答");

        InputNodeCheckpoint node = Assert.IsType<InputNodeCheckpoint>(
            checkpoint.Nodes.Single(candidate => candidate.Path == answer));

        Assert.Equal("回答内容", node.Answer);
        Assert.False(node.Waiting);
    }

    [Fact]
    public void Approval_checkpoint_carries_input_values()
    {
        TaskCheckpoint checkpoint = TaskCheckpointFixture.Create();
        NodePath approve = NodePath.Of("整体", "审批");

        ApprovalNodeCheckpoint node = Assert.IsType<ApprovalNodeCheckpoint>(
            checkpoint.Nodes.Single(candidate => candidate.Path == approve));

        Assert.False(node.Awaiting);
        Assert.Single(node.Inputs.Ordered);
        Assert.Equal("Text", node.Inputs.Ordered[0].Type);
        Assert.True(node.Inputs.Triggers[new PortName("目标")]);
    }

    [Fact]
    public void Container_checkpoint_carries_only_common_state()
    {
        TaskCheckpoint checkpoint = TaskCheckpointFixture.Create();
        NodePath container = NodePath.Of("整体");

        ContainerNodeCheckpoint node = Assert.IsType<ContainerNodeCheckpoint>(
            checkpoint.Nodes.Single(candidate => candidate.Path == container));

        Assert.Equal(1, node.Common.Revision);
        Assert.Empty(node.Common.ReworkTargets);
    }

    [Fact]
    public void Consumed_revision_allows_nullable_port()
    {
        ConsumedRevisionCheckpoint revision = new(NodePath.Of("Start"), null, 3);

        Assert.Null(revision.Port);
        Assert.Equal(3, revision.Revision);
    }
}

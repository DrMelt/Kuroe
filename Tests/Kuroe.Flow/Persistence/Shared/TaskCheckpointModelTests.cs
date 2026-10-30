using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.TestSupport;
using Xunit;

namespace Kuroe.Flow.Persistence.Shared.Tests;

/// <summary>任务检查点的数据形状：身份、流程快照、开始状态、历史与调度种子。</summary>
public sealed class TaskCheckpointModelTests
{
    [Fact]
    public void Checkpoint_document_carries_version_and_task()
    {
        TaskCheckpointDocument document = TaskCheckpointFixture.CreateDocument();

        Assert.Equal(TaskCheckpointVersion.Current, document.Version);
        Assert.Equal(new TaskId(TaskCheckpointFixture.DefaultTaskId), document.Task.Identity.Id);
    }

    [Fact]
    public void Task_checkpoint_preserves_identity_and_flow_snapshot()
    {
        TaskCheckpoint checkpoint = TaskCheckpointFixture.Create();

        Assert.Equal("检查点任务", checkpoint.Identity.Title);
        Assert.Equal(new FlowName("默认"), checkpoint.Flow.Name);
        Assert.Equal("""{"Flows":[]}""", checkpoint.Flow.Document);
        Assert.Equal(TaskLifecycle.Paused, checkpoint.Lifecycle);
        Assert.False(checkpoint.Completed);
    }

    [Fact]
    public void Task_checkpoint_preserves_start_inputs_and_texts()
    {
        TaskCheckpoint checkpoint = TaskCheckpointFixture.Create();
        PortName target = new("目标");

        Assert.Equal("目标内容", checkpoint.Start.Inputs.Text(target));
        Assert.Equal("目标内容", checkpoint.Start.StartTexts[target]);
    }

    [Fact]
    public void Task_checkpoint_preserves_journal_and_scheduler_seed()
    {
        TaskCheckpoint checkpoint = TaskCheckpointFixture.Create();

        Assert.Single(checkpoint.Journal.Entries);
        Assert.Equal("已暂停。", checkpoint.Journal.Entries[0].Text);
        Assert.Equal(3, checkpoint.Executions.Count);
        Assert.Equal(5, checkpoint.Nodes.Count);
        Assert.Equal([NodePath.Of("整体", "计划"), NodePath.Of("整体", "读取")], checkpoint.Pending.Targets);
    }

    [Fact]
    public void Task_checkpoint_allows_empty_collections()
    {
        TaskCheckpoint checkpoint = new(
            new TaskIdentity(new TaskId(1), "空任务", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch),
            new FlowSnapshot(new FlowName("空"), "{}"),
            new TaskStartSnapshot(new StartInputs([]), new Dictionary<PortName, string>()),
            TaskLifecycle.Active,
            Completed: false,
            new TaskJournalSnapshot([], 0),
            [],
            [],
            new SchedulerSeed([]));

        Assert.Empty(checkpoint.Executions);
        Assert.Empty(checkpoint.Nodes);
        Assert.Empty(checkpoint.Pending.Targets);
    }

    [Fact]
    public void Scheduler_seed_carries_target_order()
    {
        NodePath first = NodePath.Of("整体", "甲");
        NodePath second = NodePath.Of("整体", "乙");

        SchedulerSeed seed = new([first, second]);

        Assert.Equal([first, second], seed.Targets);
    }
}

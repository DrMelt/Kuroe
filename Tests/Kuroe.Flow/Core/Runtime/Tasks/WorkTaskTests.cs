using ErrorOr;
using Kuroe.Flow.Compilation.Graph;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Flow.Tests.Runtime.Tasks;

/// <summary>任务控制状态：瞬态 Done 快照不阻止暂停与终止，真正完成后才拒绝。</summary>
public sealed class WorkTaskTests
{
    [Fact]
    public void Pause_accepts_transient_done_snapshot()
    {
        WorkTask task = CreateTask();

        Assert.Equal(TaskState.Done, task.State);
        Assert.False(task.Completed);

        ErrorOr<Success> paused = task.UnderLock(() => task.Pause());

        Assert.False(paused.IsError);
        Assert.Equal(TaskState.Paused, task.State);
        Assert.False(task.Completed);
    }

    [Fact]
    public void Terminate_accepts_transient_done_snapshot()
    {
        WorkTask task = CreateTask();

        Assert.Equal(TaskState.Done, task.State);
        Assert.False(task.Completed);

        ErrorOr<Success> terminated = task.UnderLock(() => task.Terminate());

        Assert.False(terminated.IsError);
        Assert.Equal(TaskState.Terminated, task.State);
        Assert.False(task.Completed);
    }

    [Fact]
    public void Pause_rejects_completed_task()
    {
        WorkTask task = CreateTask();
        MarkCompleted(task);

        ErrorOr<Success> paused = task.UnderLock(() => task.Pause());

        Assert.True(paused.IsError);
        Assert.Equal(FlowErrorCodes.TaskDoneCannotPause, paused.FirstError.Code);
    }

    [Fact]
    public void Terminate_rejects_completed_task()
    {
        WorkTask task = CreateTask();
        MarkCompleted(task);

        ErrorOr<Success> terminated = task.UnderLock(() => task.Terminate());

        Assert.True(terminated.IsError);
        Assert.Equal(FlowErrorCodes.TaskDoneCannotTerminate, terminated.FirstError.Code);
    }

    /// <summary>构造没有可启动工作的任务，快照为 Done，但推进循环尚未标记完成。</summary>
    private static WorkTask CreateTask()
    {
        NodePath startPath = new([StartNode.ReservedLabel]);
        StartNode start = new()
        {
            Path = startPath,
            Outputs = [new Port(new PortName("目标"), PortTypes.Text)],
        };
        NodeGraph graph = new([start], []);
        FlowDefinition flow = new(
            new FlowName("默认"),
            null,
            start.Outputs,
            new Dictionary<PortName, string>(),
            NodeSpec.Define(new NodeName("整体")) with { Type = TestNodeTypes.Container });

        return new WorkTask(
            new TaskRegistry(new ExecutionIndex()),
            new TaskId(1),
            StartInputs.Single(new PortName("目标"), "目标"),
            PortTexts.Empty,
            flow,
            graph,
            "测试任务",
            onContent: null,
            onEntry: null);
    }

    private static void MarkCompleted(WorkTask task) =>
        task.UnderLock(() =>
        {
            task.MarkCompleted();
            return true;
        });
}

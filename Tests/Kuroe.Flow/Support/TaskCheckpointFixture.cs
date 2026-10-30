using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Flow.TestSupport;

/// <summary>任务检查点测试数据：一套完整的任务、节点与执行状态，供模型、编解码、存储与恢复契约测试共用。</summary>
public static class TaskCheckpointFixture
{
    /// <summary>固定的任务号。</summary>
    public const int DefaultTaskId = 7;

    /// <summary>创建一个带任务号的检查点文档。</summary>
    public static TaskCheckpointDocument CreateDocument(int taskId = DefaultTaskId) =>
        new(TaskCheckpointVersion.Current, Create(taskId));

    /// <summary>创建一个带任务号的检查点。</summary>
    public static TaskCheckpoint Create(int taskId = DefaultTaskId)
    {
        TaskId id = new(taskId);
        NodePath plan = NodePath.Of("整体", "计划");
        NodePath read = NodePath.Of("整体", "读取");
        NodePath answer = NodePath.Of("整体", "回答");
        NodePath approve = NodePath.Of("整体", "审批");
        NodePath container = NodePath.Of("整体");

        FlowName flow = new("默认");
        PortName target = new("目标");
        PortName result = new("结论");

        return new TaskCheckpoint(
            new TaskIdentity(id, "检查点任务", At(3, 4, 5), At(3, 5, 6)),
            new FlowSnapshot(flow, """{"Flows":[]}"""),
            new TaskStartSnapshot(
                StartInputs.Single(target, "目标内容"),
                new Dictionary<PortName, string> { [target] = "目标内容" }),
            TaskLifecycle.Paused,
            Completed: false,
            new TaskJournalSnapshot([new JournalEntry(At(3, 5), JournalLevel.Information, "信息", "已暂停。")], 0),
            [
                new ExecutionCheckpoint(
                    new ExecutionId(taskId * 10 + 1),
                    plan,
                    1,
                    ExecutionState.Succeeded,
                    "完成",
                    At(3, 4, 10),
                    At(3, 4, 11),
                    At(3, 4, 12),
                    [],
                    [new JournalEntry(At(3, 4, 11), JournalLevel.Information, "回复", "结果")],
                    0,
                    [new InputValueCheckpoint(target, new ContextSourceCheckpoint(ContextSourceKind.Start, null, null, target), "目标内容")],
                    new Dictionary<PortName, string> { [target] = "目标内容" }),
                new ExecutionCheckpoint(
                    new ExecutionId(taskId * 10 + 2),
                    read,
                    1,
                    ExecutionState.Failed,
                    "失败",
                    At(3, 4, 20),
                    At(3, 4, 21),
                    At(3, 4, 22),
                    ["读取失败。"],
                    [],
                    0,
                    [],
                    new Dictionary<PortName, string>()),
                new ExecutionCheckpoint(
                    new ExecutionId(taskId * 10 + 3),
                    answer,
                    1,
                    ExecutionState.Interrupted,
                    "已中断",
                    At(3, 4, 30),
                    At(3, 4, 31),
                    At(3, 4, 32),
                    ["任务已暂停。"],
                    [],
                    0,
                    [],
                    new Dictionary<PortName, string>()),
            ],
            [
                new ExecutableNodeCheckpoint(
                    plan,
                    Common(plan, revision: 2, lastInputGroup: 0, blocked: false, rerunRequested: false),
                    1,
                    new ExecutionId(taskId * 10 + 1),
                    new Dictionary<PortName, PortValue> { [result] = new("计划结果") }),
                new SelfDrivenNodeCheckpoint(
                    read,
                    Common(read, revision: 0, lastInputGroup: null, blocked: true, rerunRequested: false),
                    1,
                    null,
                    "读取失败。",
                    false,
                    false),
                new InputNodeCheckpoint(
                    answer,
                    Common(answer, revision: 1, lastInputGroup: null, blocked: false, rerunRequested: false),
                    "回答内容",
                    false),
                new ApprovalNodeCheckpoint(
                    approve,
                    Common(approve, revision: 1, lastInputGroup: 0, blocked: false, rerunRequested: false),
                    new NodeInputValuesCheckpoint(
                        [new NodeInputValueCheckpoint(target, "Text", new InputValueCheckpoint(target, new ContextSourceCheckpoint(ContextSourceKind.Start, null, null, target), "目标内容"))],
                        [target],
                        new Dictionary<PortName, bool> { [target] = true },
                        new Dictionary<PortName, string>()),
                    false),
                new ContainerNodeCheckpoint(
                    container,
                    Common(container, revision: 1, lastInputGroup: null, blocked: false, rerunRequested: false)),
            ],
            new SchedulerSeed([plan, read]));
    }

    private static DateTimeOffset At(int hour, int minute, int second = 0) =>
        new(2026, 1, 2, hour, minute, second, TimeSpan.Zero);

    private static NodeCommonCheckpoint Common(
        NodePath path,
        long revision,
        int? lastInputGroup,
        bool blocked,
        bool rerunRequested) =>
        new(
            revision,
            lastInputGroup,
            [new ConsumedRevisionCheckpoint(NodePath.Of("Start"), null, 1)],
            blocked ? [path] : [],
            blocked,
            rerunRequested);
}

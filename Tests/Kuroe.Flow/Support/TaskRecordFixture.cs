using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Persistence.Records.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Flow.TestSupport;

/// <summary>任务存档测试数据：一套完整的任务、节点与执行状态，供模型、编解码、存储与恢复契约测试共用。</summary>
public static class TaskRecordFixture
{
    /// <summary>固定的任务号。</summary>
    public const int DefaultTaskId = 7;

    /// <summary>创建一个带任务号的存档。</summary>
    public static TaskRecord Create(int taskId = DefaultTaskId)
    {
        TaskId id = new(taskId);
        NodePath plan = NodePath.Of("整体", "计划");
        NodePath read = NodePath.Of("整体", "读取");
        NodePath answer = NodePath.Of("整体", "回答");
        NodePath approve = NodePath.Of("整体", "审批");
        NodePath container = NodePath.Of("整体");

        FlowName flow = new("默认");
        PortName target = new("目标");

        return new TaskRecord(
            new TaskIdentity(id, "存档任务", At(3, 4, 5), At(3, 5, 6)),
            new FlowSnapshot(flow, """{"Nodes":[]}""", """{"Flows":[]}"""),
            new TaskStartSnapshot(
                StartInputs.Single(target, "目标内容"),
                new Dictionary<PortName, string> { [target] = "目标内容" }),
            TaskLifecycle.Paused,
            Completed: false,
            new TaskJournalSnapshot([new JournalEntry(At(3, 5), JournalLevel.Information, "信息", "已暂停。")], 0),
            [
                new ExecutionRecord(
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
                    [new InputValueRecord(target, new ContextSourceRecord(ContextSourceKind.Start, null, null, target), "目标内容")],
                    new Dictionary<PortName, string> { [target] = "目标内容" }),
                new ExecutionRecord(
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
                new ExecutionRecord(
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
                new ExecutableNodeRecord(
                    plan,
                    Common(
                        revision: 2,
                        lastInputGroup: 0,
                        rerunRequested: false,
                        publication: new PublicationRecord(
                            new Dictionary<PortName, bool> { [new PortName("结论")] = false },
                            new Dictionary<PortName, IReadOnlyList<PublicationValueRecord>>
                            {
                                [new PortName("结论")] =
                                [
                                    new PublicationValueRecord(
                                        new ContextSourceRecord(
                                            ContextSourceKind.Execution,
                                            new ExecutionId(taskId * 10 + 1),
                                            plan,
                                            new PortName("结论")),
                                        "计划结果"),
                                ],
                            })),
                    [],
                    false),
                new SelfDrivenNodeRecord(
                    read,
                    Common(revision: 0, lastInputGroup: null, rerunRequested: false),
                    "读取失败。",
                    false),
                new InputNodeRecord(
                    answer,
                    Common(revision: 1, lastInputGroup: null, rerunRequested: false),
                    false),
                new ApprovalNodeRecord(
                    approve,
                    Common(revision: 1, lastInputGroup: 0, rerunRequested: false),
                    new NodeInputValuesRecord(
                        [new NodeInputValueRecord(target, "Text", new InputValueRecord(target, new ContextSourceRecord(ContextSourceKind.Start, null, null, target), "目标内容"))]),
                    false),
                new ContainerNodeRecord(
                    container,
                    Common(revision: 1, lastInputGroup: null, rerunRequested: false)),
            ],
            new SchedulerSeed([plan, read]));
    }

    private static DateTimeOffset At(int hour, int minute, int second = 0) =>
        new(2026, 1, 2, hour, minute, second, TimeSpan.Zero);

    private static NodeCommonRecord Common(
        long revision,
        int? lastInputGroup,
        bool rerunRequested,
        PublicationRecord? publication = null) =>
        new(
            revision,
            1,
            lastInputGroup,
            [new ConsumedRevisionRecord(NodePath.Of("Start"), null, 1)],
            publication,
            rerunRequested);
}

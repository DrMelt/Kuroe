using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Execution.Records;
using Kuroe.Flow.Graph.Records;
using Kuroe.Flow.Tasks.Records;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
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
            At(3, 5, 7),
            TaskLifecycle.Paused,
            Completed: false,
            new TaskJournalRecord([new JournalEntry(At(3, 5), JournalLevel.Information, "信息", "已暂停。")], 0),
            new FlowRecord(
                FlowRecordKinds.Graph,
                new GraphFlowDefinitionRecord(flow, """{"Nodes":[]}""", """{"Flows":[]}"""),
                new GraphFlowStateRecord(
                    new TaskStartSnapshot(
                        StartInputs.Single(target, "目标内容"),
                        new Dictionary<PortName, string> { [target] = "目标内容" }),
                    [
                        new ExecutableNodeRecord(
                            plan,
                            Common(
                                revision: 2,
                                lastInputGroup: 0,
                                rerunRequested: false,
                                activePublicationRevision: 2),
                            [],
                            null),
                        new ExecutableNodeRecord(
                            read,
                            Common(revision: 0, lastInputGroup: null, rerunRequested: false),
                            [read],
                            new NodeBlock("读取失败。", new ExecutionId(taskId * 10 + 2), At(3, 4, 22))),
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
                    [
                        new PortPublicationRecord(
                            plan,
                            new PortName("结论"),
                            1,
                            At(3, 4, 9),
                            true,
                            [new PublicationValueRecord(
                                new ContextSourceRecord(
                                    ContextSourceKind.Execution,
                                    new ExecutionId(taskId * 10 + 1),
                                    plan,
                                    new PortName("结论")),
                                "旧计划结果")]),
                        new PortPublicationRecord(
                            plan,
                            new PortName("结论"),
                            2,
                            At(3, 4, 13),
                            false,
                            [new PublicationValueRecord(
                                new ContextSourceRecord(
                                    ContextSourceKind.Execution,
                                    new ExecutionId(taskId * 10 + 1),
                                    plan,
                                    new PortName("结论")),
                                "计划结果")]),
                    ],
                    new SchedulerSeed([plan, read]))),
            [
                new ExecutionRecord(
                    new Kuroe.Flow.Execution.ExecutionId(taskId * 10 + 1),
                    GraphRecordTargets.Format(plan),
                    1,
                    Kuroe.Flow.Execution.ExecutionState.Succeeded,
                    "完成",
                    At(3, 4, 10),
                    At(3, 4, 11),
                    At(3, 4, 12),
                    [],
                    new ExecutionUsageRecord(2, 0, new TokenUsageRecord(120, 30, 150, 80, 5), 90),
                    [
                        new ExecutionInputRecord(
                            new Kuroe.Flow.Execution.ExecutionPortName(target.Value),
                            GraphRecordSources.Write(new StartSource(target)),
                            "目标内容"),
                    ],
                    new Dictionary<Kuroe.Flow.Execution.ExecutionPortName, string>
                    {
                        [new Kuroe.Flow.Execution.ExecutionPortName(target.Value)] = "目标内容",
                    }),
                new ExecutionRecord(
                    new Kuroe.Flow.Execution.ExecutionId(taskId * 10 + 2),
                    GraphRecordTargets.Format(read),
                    1,
                    Kuroe.Flow.Execution.ExecutionState.Failed,
                    "失败",
                    At(3, 4, 20),
                    At(3, 4, 21),
                    At(3, 4, 22),
                    ["读取失败。"],
                    new ExecutionUsageRecord(0, 0, new TokenUsageRecord(null, null, null, null, null), null),
                    [],
                    new Dictionary<Kuroe.Flow.Execution.ExecutionPortName, string>()),
                new ExecutionRecord(
                    new Kuroe.Flow.Execution.ExecutionId(taskId * 10 + 3),
                    GraphRecordTargets.Format(answer),
                    1,
                    Kuroe.Flow.Execution.ExecutionState.Interrupted,
                    "已中断",
                    At(3, 4, 30),
                    At(3, 4, 31),
                    At(3, 4, 32),
                    ["任务已暂停。"],
                    new ExecutionUsageRecord(0, 0, new TokenUsageRecord(null, null, null, null, null), null),
                    [],
                    new Dictionary<Kuroe.Flow.Execution.ExecutionPortName, string>()),
            ]);
    }

    private static DateTimeOffset At(int hour, int minute, int second = 0) =>
        new(2026, 1, 2, hour, minute, second, TimeSpan.Zero);

    private static NodeCommonRecord Common(
        long revision,
        int? lastInputGroup,
        bool rerunRequested,
        long? activePublicationRevision = null) =>
        new(
            revision,
            1,
            lastInputGroup,
            [new ConsumedRevisionRecord(NodePath.Of("Start"), null, 1)],
            activePublicationRevision,
            rerunRequested);
}

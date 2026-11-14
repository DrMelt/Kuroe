using Kuroe.Execution.Shared;
using Kuroe.Presentation.Shared;
using Kuroe.Tasks.Shared;

namespace Kuroe.Tasks.Presentation.Shared;

/// <summary>任务在某一刻的只读形状：流程视图、过程记录与全部执行。渲染时不再回读可变成。</summary>
public sealed record TaskSnapshot(
    TaskId Id,
    string Title,
    StartInputs Inputs,
    IFlowView View,
    TaskState State,
    int LiveExecutions,
    IReadOnlyList<JournalEntry> Journal,
    int DroppedJournal,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt)
{
    /// <summary>中性执行快照，按提交顺序。</summary>
    public IReadOnlyList<ExecutionSnapshot> Executions { get; init; } = [];

    /// <summary>此刻等待外部输入的步骤。</summary>
    public IReadOnlyList<PendingStepView> Pending { get; init; } = [];

    /// <summary>任务启动时的开始端口文本。</summary>
    public FlowTexts? StartTexts { get; init; }

    /// <summary>流程的执行节点数。</summary>
    public int TotalExecutableNodes => View.Steps.Count;

    /// <summary>已发布产出的最远执行节点在执行节点表里的位置，用于列表里的进度。</summary>
    public int FrontierNodes =>
        View.Steps
            .Select((step, index) => (step, index))
            .Where(entry => entry.step.State == FlowNodeState.Done)
            .Select(entry => entry.index + 1)
            .DefaultIfEmpty(0)
            .Max();

    /// <summary>执行节点在执行节点表里的顺位，用于展示；容器或未收录的节点给最末顺位之后。</summary>
    public int OrdinalOf(string path) =>
        View.IndexOf(path) is { } index ? index + 1 : View.Steps.Count + 1;
}
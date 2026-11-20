using Kuroe.Kernel.Presentation.Shared;
using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Kernel.Execution.Shared.Journal;
using Kuroe.Kernel.Execution.Shared.State;

namespace Kuroe.Kernel.Tasks.Presentation.Shared;

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

    /// <summary>流程的步骤数。</summary>
    public int TotalSteps => View.Steps.Count;

    /// <summary>已发布产出的最远步骤在步骤表里的位置，用于列表里的进度。</summary>
    public int ProgressSteps =>
        View.Steps
            .Select((step, index) => (step, index))
            .Where(entry => entry.step.State == FlowItemState.Done)
            .Select(entry => entry.index + 1)
            .DefaultIfEmpty(0)
            .Max();

    /// <summary>步骤在步骤表里的顺位，用于展示；分组或未收录的步骤给最末顺位之后。</summary>
    public int OrdinalOf(string path) =>
        View.IndexOf(path) is { } index ? index + 1 : View.Steps.Count + 1;

    /// <summary>该执行在所属目标上的轮次，按执行序列计算。</summary>
    public int RoundOf(ExecutionSnapshot execution)
    {
        int round = 0;
        foreach (ExecutionSnapshot candidate in Executions)
        {
            if (candidate.Request.Step == execution.Request.Step)
            {
                round++;
            }

            if (candidate.Id == execution.Id)
            {
                return round;
            }
        }

        return round;
    }
}
using Kuroe.Agent;
using Kuroe.Agent.Turns;
using Kuroe.Workflows.Flows;

namespace Kuroe.Workflows.Tasks;

/// <summary>任务在某一刻的只读形状：步骤、单元、agent 与前台对话都在里面，渲染时不再回读可变成。</summary>
public sealed record TaskSnapshot(
    TaskId Id,
    string Title,
    string Goal,
    Workflow Flow,
    TaskState State,
    int DialogueTurns,
    int LiveRuns,
    StepPlan? Plan,
    IReadOnlyList<StepSnapshot> Steps,
    IReadOnlyList<UnitSnapshot> Units,
    IReadOnlyList<JournalEntry> Dialogue,
    int DroppedDialogue,
    DateTimeOffset LastActivityAt)
{
    /// <summary>流程的步骤数。</summary>
    public int TotalSteps => Flow.Count;

    /// <summary>全部单元推进到的最远步骤位置，用于列表里的进度。</summary>
    public int FrontierSteps => Units.Count == 0 ? 0 : Units.Max(unit => unit.StepCursor);
}

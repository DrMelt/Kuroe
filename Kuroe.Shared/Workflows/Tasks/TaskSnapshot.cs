using Kuroe.Shared.Agent;
using Kuroe.Shared.Agent.Turns;
using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Workflows.Tasks;

/// <summary>任务在某一刻的只读形状：节点、单元、agent 与前台对话都在里面，渲染时不再回读可变成。</summary>
public sealed record TaskSnapshot(
    TaskId Id,
    string Title,
    string Goal,
    Workflow Flow,
    NodeGraph Graph,
    TaskState State,
    int DialogueTurns,
    int LiveRuns,
    IReadOnlyDictionary<int, PlanOutput> Splits,
    IReadOnlyList<NodeSnapshot> Nodes,
    IReadOnlyList<UnitSnapshot> Units,
    IReadOnlyList<JournalEntry> Dialogue,
    int DroppedDialogue,
    DateTimeOffset LastActivityAt)
{
    /// <summary>流程的叶子数。</summary>
    public int TotalNodes => Graph.Count;

    /// <summary>全部单元推进到的最远叶子位置，用于列表里的进度。</summary>
    public int FrontierNodes => Units.Count == 0 ? 0 : Units.Max(unit => unit.NodeCursor);
}

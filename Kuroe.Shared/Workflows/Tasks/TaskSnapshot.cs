using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Turns;
using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Workflows.Tasks;

/// <summary>任务在某一刻的只读形状：节点、执行节点状态、条目结论与前台对话都在里面，渲染时不再回读可变成。</summary>
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
    IReadOnlyList<NodeStateSnapshot> NodeStates,
    IReadOnlyList<ItemStateSnapshot> ItemStates,
    IReadOnlyList<JournalEntry> Dialogue,
    int DroppedDialogue,
    DateTimeOffset LastActivityAt)
{
    /// <summary>流程的执行节点数。</summary>
    public int TotalNodes => Graph.Count;

    /// <summary>已发布产出的最远执行节点序号，用于列表里的进度。</summary>
    public int FrontierNodes => NodeStates.Count == 0 ? 0 : NodeStates
        .Where(state => state.State == NodeState.Done)
        .Select(state => state.Index + 1)
        .DefaultIfEmpty(0)
        .Max();
}

using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Turns;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Graph;

namespace Kuroe.Shared.Workflows.Tasks;

/// <summary>任务在某一刻的只读形状：节点、执行节点状态、容器状态与前台对话都在里面，渲染时不再回读可变成。</summary>
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
    IReadOnlyList<ExecutableSnapshot> Executables,
    IReadOnlyList<ExecutableStateSnapshot> ExecutableStates,
    IReadOnlyList<ContainerSnapshot> Containers,
    IReadOnlyList<JournalEntry> Dialogue,
    int DroppedDialogue,
    DateTimeOffset LastActivityAt)
{
    /// <summary>流程的执行节点数。</summary>
    public int TotalNodes => Graph.TotalExecutables;

    /// <summary>已发布产出的最远执行节点在执行节点表里的位置，用于列表里的进度。</summary>
    public int FrontierNodes
    {
        get
        {
            if (ExecutableStates.Count == 0)
            {
                return 0;
            }

            Dictionary<int, int> rankByIndex = Graph.ExecutableNodes
                .Select((executable, rank) => (executable.Index, rank))
                .ToDictionary(pair => pair.Index, pair => pair.rank);

            return ExecutableStates
                .Where(state => state.State == NodeState.Done)
                .Select(state => rankByIndex.GetValueOrDefault(state.Index) + 1)
                .DefaultIfEmpty(0)
                .Max();
        }
    }

    /// <summary>执行节点在执行节点表里的顺位，用于展示；容器或未收录的序号给最末顺位之后。</summary>
    public int OrdinalOf(int executableIndex)
    {
        for (int i = 0; i < Graph.ExecutableNodes.Count; i++)
        {
            if (Graph.ExecutableNodes[i].Index == executableIndex)
            {
                return i + 1;
            }
        }

        return Graph.ExecutableNodes.Count + 1;
    }
}

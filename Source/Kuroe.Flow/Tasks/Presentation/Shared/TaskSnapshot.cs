using Kuroe.Flow.Presentation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;

namespace Kuroe.Flow.Shared.RuntimeGraph.Tasks;

/// <summary>任务在某一刻的只读形状：流程视图、执行节点状态、容器状态与过程记录都在里面，渲染时不再回读可变成。</summary>
public sealed record TaskSnapshot(
    TaskId Id,
    string Title,
    StartInputs Inputs,
    IPortTexts StartTexts,
    IFlowView View,
    TaskState State,
    int LiveExecutions,
    IReadOnlyList<ExecutableSnapshot> Executables,
    IReadOnlyList<ExecutableStateSnapshot> ExecutableStates,
    IReadOnlyList<ContainerSnapshot> Containers,
    IReadOnlyList<JournalEntry> Journal,
    int DroppedJournal,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt)
{
    /// <summary>流程的执行节点数。</summary>
    public int TotalExecutableNodes => Executables.Count;

    /// <summary>已发布产出的最远执行节点在执行节点表里的位置，用于列表里的进度。</summary>
    public int FrontierNodes =>
        ExecutableStates
            .Where(state => state.State == NodeState.Done)
            .Select(state => OrdinalOf(state.Node))
            .DefaultIfEmpty(0)
            .Max();

    /// <summary>执行节点在执行节点表里的顺位，用于展示；容器或未收录的节点给最末顺位之后。</summary>
    public int OrdinalOf(NodePath executable) =>
        View.IndexOf(executable.Value) is { } index ? index + 1 : View.Steps.Count + 1;
}

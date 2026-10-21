using ErrorOr;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.InputChannel;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Node.Approval;

namespace Kuroe.Pending;

/// <summary>一个待处理节点：节点路径与等待种类。</summary>
public readonly record struct PendingNode(NodePath Node, PendingKind Kind);

/// <summary>等待型节点的宿主功能：定位待处理节点、点名与歧义判定、交付外部输入并推进下游。
/// 外部输入经协议交到节点上，登记即代表收下。</summary>
public sealed class PendingService
{
    private readonly TaskRegistry _registry;
    private readonly IFlowRunner _runner;
    private readonly IInputDelivery _inputs;

    internal PendingService(TaskRegistry registry, IFlowRunner runner, IInputDelivery inputs)
    {
        _registry = registry;
        _runner = runner;
        _inputs = inputs;
    }

    /// <summary>任务里此刻待处理的节点，按图先根序。</summary>
    public ErrorOr<List<PendingNode>> Pending(TaskId id)
    {
        ErrorOr<WorkTask> found = _registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask task = found.Value;

        return task.UnderLock(() => Pending(task));
    }

    /// <summary>回答待输入的节点：nodePath 为空时回答唯一待输入节点，多个时必须点名。回答即产出并放行下游，空回答不构成输入。</summary>
    public ErrorOr<Success> Answer(TaskId id, string? nodePath, string input) =>
        string.IsNullOrWhiteSpace(input)
            ? [PendingErrors.EmptyAnswer()]
            : Take(id, PendingKind.Answer, nodePath, input, null);

    /// <summary>批准待批准的节点：nodes 为空时批准全部待批准节点，给出节点路径时只批准其中的节点。</summary>
    public ErrorOr<Success> Approve(TaskId id, IReadOnlyList<NodePath>? nodes = null) =>
        Take(id, PendingKind.Approve, null, string.Empty, nodes);

    /// <summary>交付一次外部输入：取给定等待种类的节点，点名与歧义在此判定，随后推进受影响的节点。取消的任务不再交付。</summary>
    private ErrorOr<Success> Take(
        TaskId id,
        PendingKind kind,
        string? nodePath,
        string input,
        IReadOnlyList<NodePath>? nodes)
    {
        ErrorOr<WorkTask> found = _registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask task = found.Value;
        ErrorOr<List<NodePath>> accepted = task.UnderLock<ErrorOr<List<NodePath>>>(() =>
        {
            if (task.State is TaskState.Canceled)
            {
                return [PendingErrors.Canceled(id, kind)];
            }

            List<NodePath> awaiting = [.. Pending(task).Where(entry => entry.Kind == kind).Select(entry => entry.Node)];
            if (nodes is { Count: > 0 })
            {
                awaiting = [.. awaiting.Where(node => nodes.Contains(node))];
            }

            if (nodePath is { } path)
            {
                int index = awaiting.FindIndex(node => node.Value == path);
                if (index < 0)
                {
                    return [PendingErrors.NoAwaitingNode(path)];
                }

                awaiting = [awaiting[index]];
            }
            else if (awaiting.Count == 0)
            {
                return [PendingErrors.NoAwaiting(id, kind)];
            }
            else if (kind == PendingKind.Answer && awaiting.Count > 1)
            {
                return [PendingErrors.AmbiguousInput(id, awaiting.Count)];
            }

            Deliver(task, awaiting, input);

            return awaiting;
        });

        if (accepted.IsError)
        {
            return accepted.ErrorsOrEmptyList;
        }

        _runner.Resume(task, accepted.Value);

        return Result.Success;
    }

    /// <summary>把一次外部输入交给这些节点。交付不表达接受与否，逐个收下。要求持有 Gate。</summary>
    private void Deliver(WorkTask task, IReadOnlyList<NodePath> nodes, string input)
    {
        foreach (NodePath node in nodes)
        {
            _inputs.Deliver(task.Context, node, input);
        }
    }

    /// <summary>任务里此刻待处理的节点，按图先根序。要求持有 Gate。</summary>
    private List<PendingNode> Pending(WorkTask task)
    {
        INodeTask access = task;
        HashSet<NodePath> accepting = [.. _inputs.Accepting(task.Context)];

        return
        [
            .. access.Graph.ExecutableNodes
                .Where(node => accepting.Contains(node.Path))
                .Select(node => new PendingNode(node.Path, node.Type is ApprovalNodeType ? PendingKind.Approve : PendingKind.Answer)),
        ];
    }
}

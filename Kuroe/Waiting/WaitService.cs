using ErrorOr;
using Kuroe.Flow.Execution.Tasks;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.Shared.Waiting;

namespace Kuroe.Waiting;

/// <summary>一个待处理节点：序号、节点名与等待种类。</summary>
public readonly record struct PendingNode(int Index, string Name, AwaitKind Kind);

/// <summary>等待型节点的宿主功能：定位待处理节点、点名与歧义判定、交付外部输入并推进下游。
/// 接收外部输入是扩展实现，引擎只提供节点上下文与推进信号。</summary>
public sealed class WaitService
{
    private readonly TaskRegistry _registry;
    private readonly IFlowRunner _runner;

    internal WaitService(TaskRegistry registry, IFlowRunner runner)
    {
        _registry = registry;
        _runner = runner;
    }

    /// <summary>任务里此刻待处理的节点，按节点序号升序。</summary>
    public ErrorOr<List<PendingNode>> Pending(TaskId id)
    {
        ErrorOr<WorkTask> found = _registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask task = found.Value;

        return task.UnderLock(() =>
            Awaiting(task, null).Select(entry => new PendingNode(entry.Index, entry.Name, entry.Node.Kind)).ToList());
    }

    /// <summary>回答待输入的节点：nodeName 为空时回答唯一待输入节点，多个时必须点名。回答即产出并放行下游。</summary>
    public ErrorOr<Success> Answer(TaskId id, string? nodeName, string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [WaitErrors.EmptyAnswer()];
        }

        return Accept(id, AwaitKind.Answer, nodeName, input, null);
    }

    /// <summary>批准待批准的节点：nodes 为空时批准全部待批准节点，给出序号时只批准其中的节点。</summary>
    public ErrorOr<Success> Approve(TaskId id, IReadOnlyList<int>? nodes = null) =>
        Accept(id, AwaitKind.Approve, null, string.Empty, nodes);

    /// <summary>交付一次外部输入：取给定种类下停止等待的节点，点名与歧义在此判定，随后推进受影响的节点。</summary>
    private ErrorOr<Success> Accept(
        TaskId id,
        AwaitKind kind,
        string? nodeName,
        string input,
        IReadOnlyList<int>? nodes)
    {
        ErrorOr<WorkTask> found = _registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask task = found.Value;
        ErrorOr<List<int>> accepted = task.UnderLock<ErrorOr<List<int>>>(() =>
        {
            List<(int Index, string Name, IWaitNode Node)> awaiting = [.. Awaiting(task, kind)];
            if (nodes is { Count: > 0 })
            {
                awaiting = [.. awaiting.Where(entry => nodes.Contains(entry.Index))];
            }

            if (nodeName is { } name)
            {
                foreach ((int index, string label, IWaitNode node) in awaiting)
                {
                    if (label == name)
                    {
                        node.Receive(input);

                        return new List<int> { index };
                    }
                }

                return [WaitErrors.NoAwaitingNode(name)];
            }

            if (awaiting.Count == 0)
            {
                return [WaitErrors.NoAwaiting(id, kind)];
            }

            if (kind == AwaitKind.Answer && awaiting.Count > 1)
            {
                return [WaitErrors.AmbiguousInput(id, awaiting.Count)];
            }

            foreach ((int _, string _, IWaitNode node) in awaiting)
            {
                node.Receive(input);
            }

            return awaiting.Select(entry => entry.Index).ToList();
        });

        if (accepted.IsError)
        {
            return accepted.ErrorsOrEmptyList;
        }

        _runner.Resume(task, accepted.Value);

        return Result.Success;
    }

    /// <summary>任务里停在给定等待种类的节点，按节点序号升序。kind 为空时取全部等待型节点。随任务取消的节点不再接受输入。</summary>
    private static IEnumerable<(int Index, string Name, IWaitNode Node)> Awaiting(WorkTask task, AwaitKind? kind)
    {
        INodeTask access = task;
        foreach (GraphNode node in access.Graph.Nodes)
        {
            if (access.Context.Materialized(node.Index) is NodeRuntime runtime
                && !runtime.Canceled
                && runtime is IWaitNode wait
                && wait.Awaiting
                && (kind is not { } expected || wait.Kind == expected))
            {
                yield return (node.Index, node.Name.Value, wait);
            }
        }
    }
}

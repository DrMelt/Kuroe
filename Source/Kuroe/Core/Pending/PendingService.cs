using ErrorOr;
using Kuroe.Flow.Control;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared.Model;

namespace Kuroe.Pending;

/// <summary>一个待处理节点：步骤路径、等待种类与等待时的提问。</summary>
public readonly record struct PendingNode(FlowStepPath Step, PendingKind Kind, string? Question);

/// <summary>等待型节点的宿主功能：定位待处理节点、点名与歧义判定、交付外部输入并唤醒推进。</summary>
public sealed class PendingService(TaskRegistry registry, Kuroe.Flow.Runtime.Engine.IFlowRuntime runner)
{
    private readonly Kuroe.Flow.Runtime.Engine.IFlowRuntime _runner = runner;
    /// <summary>任务里此刻待处理的节点，按流程视图顺序，带等待时的提问。</summary>
    public ErrorOr<List<PendingNode>> Pending(TaskId id)
    {
        ErrorOr<WorkTask> found = registry.Find(id);
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
    public ErrorOr<Success> Approve(TaskId id, IReadOnlyList<FlowStepPath>? nodes = null) =>
        Take(id, PendingKind.Approve, null, string.Empty, nodes);

    /// <summary>交付一次外部输入：取给定等待种类的节点，点名与歧义在此判定，交付由控制实例唤醒推进。</summary>
    private ErrorOr<Success> Take(
        TaskId id,
        PendingKind kind,
        string? nodePath,
        string input,
        IReadOnlyList<FlowStepPath>? nodes)
    {
        ErrorOr<WorkTask> found = registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask task = found.Value;
        ErrorOr<List<PendingNode>> accepted = task.UnderLock<ErrorOr<List<PendingNode>>>(() =>
        {
            if (task.Lifecycle == TaskLifecycle.Terminated)
            {
                return [PendingErrors.Terminated(id, kind)];
            }

            if (task.Lifecycle == TaskLifecycle.Paused)
            {
                return [PendingErrors.Paused(id, kind)];
            }

            if (task.Control is not { } control)
            {
                return [PendingErrors.NoAwaiting(id, kind)];
            }

            List<PendingNode> awaiting = [.. Pending(task).Where(entry => entry.Kind == kind)];
            if (nodes is { Count: > 0 })
            {
                awaiting = [.. awaiting.Where(entry => nodes.Contains(entry.Step))];
            }

            if (nodePath is { } path)
            {
                int index = awaiting.FindIndex(entry => entry.Step.Value == path);
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

            foreach (PendingNode entry in awaiting)
            {
                ErrorOr<Success> delivered = control.Deliver(entry.Step, input);
                if (delivered.IsError)
                {
                    return delivered.ErrorsOrEmptyList;
                }
            }

            return awaiting;
        });

        if (accepted.IsError)
        {
            return accepted.ErrorsOrEmptyList;
        }

        _runner.Advance(task, [.. accepted.Value.Select(node => node.Step)]);

        return Result.Success;
    }

    /// <summary>任务里此刻待处理的节点，带等待时的提问。要求持有 Gate。</summary>
    private static List<PendingNode> Pending(WorkTask task)
    {
        if (task.Control is not { } control)
        {
            return [];
        }

        return
        [
            .. control.Pending().Select(step => new PendingNode(
                step.Step,
                step.Kind == FlowPendingKind.Approve ? PendingKind.Approve : PendingKind.Answer,
                step.Question)),
        ];
    }
}
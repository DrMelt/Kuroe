using ErrorOr;
using Kuroe.Flow.Control;
using Kuroe.Flow.Runtime.Tasks;
using LoopExecution = Kuroe.Flow.Execution.Execution;
using ExecutionId = Kuroe.Flow.Execution.ExecutionId;
using ExecutionRequest = Kuroe.Flow.Execution.ExecutionRequest;

namespace Kuroe.Flow.Runtime;

/// <summary>执行号与执行归属的全局索引：执行号在全部任务域间递增，按号取执行时一并交出归属任务。</summary>
public sealed class ExecutionIndex : IFlowExecutionGateway
{
    private readonly Lock _gate = new();
    private readonly Dictionary<ExecutionId, Entry> _entries = [];
    private int _nextExecution = 1;

    /// <summary>为一个任务建立执行并登记归属。</summary>
    public LoopExecution Open(ITaskFlowContext owner, ExecutionRequest request)
    {
        lock (_gate)
        {
            LoopExecution execution = new(new ExecutionId(_nextExecution++), request);
            _entries[execution.Id] = new Entry(execution, owner);

            return execution;
        }
    }

    /// <summary>登记一个从存档恢复的执行，推进下一次执行号。</summary>
    public void Restore(ITaskFlowContext owner, LoopExecution execution)
    {
        lock (_gate)
        {
            _entries[execution.Id] = new Entry(execution, owner);
            _nextExecution = Math.Max(_nextExecution, execution.Id.Value + 1);
        }
    }

    /// <summary>按执行号取执行，不存在时返回错误。</summary>
    public ErrorOr<LoopExecution> Find(ExecutionId id)
    {
        lock (_gate)
        {
            return _entries.TryGetValue(id, out Entry entry) ? entry.Execution : [ExecutionErrors.ExecutionNotFound(id.Value)];
        }
    }

    /// <summary>按执行号取执行与它的归属任务，不存在时返回错误。</summary>
    public ErrorOr<(LoopExecution Execution, ITaskFlowContext Owner)> FindOwned(ExecutionId id)
    {
        lock (_gate)
        {
            return _entries.TryGetValue(id, out Entry entry)
                ? (entry.Execution, entry.Owner)
                : [ExecutionErrors.ExecutionNotFound(id.Value)];
        }
    }

    /// <summary>停止一个已启动的执行。未启动的执行不构成一次尝试，不能停止。</summary>
    public ErrorOr<Success> Stop(ITaskFlowContext owner, ExecutionId id)
    {
        LoopExecution? execution = owner.UnderLock(() =>
            owner.Executions.FirstOrDefault(candidate => candidate.Id == id));
        if (execution is null)
        {
            return [ExecutionErrors.ExecutionNotFound(id.Value)];
        }

        if (!execution.IsLive)
        {
            return [ExecutionErrors.ExecutionSettled(execution.Id, "停止")];
        }

        if (execution.Snapshot().StartedAt is null)
        {
            return [ExecutionErrors.ExecutionNotStarted(execution.Id)];
        }

        execution.Interrupt("已停止执行。");

        return Result.Success;
    }

    /// <summary>移除一个已清空任务的执行条目。</summary>
    public void Remove(ExecutionId id)
    {
        lock (_gate)
        {
            _entries.Remove(id);
        }
    }

    private readonly record struct Entry(LoopExecution Execution, ITaskFlowContext Owner);
}
using ErrorOr;
using Kuroe.Flow.Control.Shared;
using Kuroe.Flow.Execution.Shared;

namespace Kuroe.Flow.Runtime;

/// <summary>执行号与执行归属的全局索引：执行号在全部任务域间递增，按号取执行时一并交出归属任务。</summary>
public sealed class ExecutionIndex : IExecutionOwnerIndex
{
    private readonly Lock _gate = new();
    private readonly Dictionary<ExecutionId, Entry> _entries = [];
    private int _nextExecution = 1;

    /// <summary>为一个任务建立执行并登记归属。</summary>
    public IExecution Open(
        ITaskFlowContext owner,
        ExecutionRequest request,
        Func<ExecutionId, ExecutionRequest, IExecution> create)
    {
        lock (_gate)
        {
            ExecutionId id = new(_nextExecution++);
            IExecution execution = create(id, request);
            _entries[id] = new Entry(execution, owner);

            return execution;
        }
    }

    /// <summary>登记一个从存档恢复的执行，推进下一次执行号。</summary>
    public void Restore(ITaskFlowContext owner, IExecution execution)
    {
        lock (_gate)
        {
            _entries[execution.Id] = new Entry(execution, owner);
            _nextExecution = Math.Max(_nextExecution, execution.Id.Value + 1);
        }
    }

    /// <summary>按执行号取执行与它的归属任务，不存在时返回错误。</summary>
    public ErrorOr<(IExecution Execution, ITaskFlowContext Owner)> FindOwned(ExecutionId id)
    {
        lock (_gate)
        {
            return _entries.TryGetValue(id, out Entry entry)
                ? (entry.Execution, entry.Owner)
                : [ExecutionErrors.ExecutionNotFound(id.Value)];
        }
    }

    /// <summary>移除一个已清空任务的执行条目。</summary>
    public void Remove(ExecutionId id)
    {
        lock (_gate)
        {
            _entries.Remove(id);
        }
    }

    private readonly record struct Entry(IExecution Execution, ITaskFlowContext Owner);
}
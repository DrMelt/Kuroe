using ErrorOr;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Runtime;

/// <summary>执行号与执行归属的全局索引：执行号在全部任务域间递增，按号取执行时一并交出归属任务。</summary>
public sealed class ExecutionIndex
{
    private readonly Lock _gate = new();
    private readonly Dictionary<ExecutionId, Entry> _entries = [];
    private int _nextExecution = 1;

    /// <summary>为一个任务建立执行并登记归属。</summary>
    internal Execution Create(WorkTask owner, ExecutionInput input)
    {
        lock (_gate)
        {
            Execution execution = new(new ExecutionId(_nextExecution++), input);
            _entries[execution.Id] = new Entry(execution, owner);

            return execution;
        }
    }

    /// <summary>按执行号取执行，不存在时返回错误。</summary>
    public ErrorOr<Execution> Find(ExecutionId id)
    {
        lock (_gate)
        {
            return _entries.TryGetValue(id, out Entry entry) ? entry.Execution : [ExecutionErrors.ExecutionNotFound(id.Value)];
        }
    }

    /// <summary>按执行号取执行与它的归属任务，不存在时返回错误。</summary>
    public ErrorOr<(Execution Execution, WorkTask Owner)> FindOwned(ExecutionId id)
    {
        lock (_gate)
        {
            return _entries.TryGetValue(id, out Entry entry)
                ? (entry.Execution, entry.Owner)
                : [ExecutionErrors.ExecutionNotFound(id.Value)];
        }
    }

    /// <summary>移除一个已清空任务的执行条目。</summary>
    internal void Remove(ExecutionId id)
    {
        lock (_gate)
        {
            _entries.Remove(id);
        }
    }

    private readonly record struct Entry(Execution Execution, WorkTask Owner);
}
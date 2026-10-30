using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Persistence.Shared;

/// <summary>任务状态变更的接收端：运行期只标记需要写盘，写盘时机由实现决定。</summary>
public interface ITaskChangeSink
{
    /// <summary>标记一个任务的状态已变。</summary>
    void MarkDirty(TaskId id);
}

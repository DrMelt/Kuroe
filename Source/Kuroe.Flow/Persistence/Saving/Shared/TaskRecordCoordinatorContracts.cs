using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Persistence.Saving.Shared;

/// <summary>任务存档协调：接收变更标记，合并并写入存档。</summary>
public interface ITaskRecordCoordinator
{
    /// <summary>标记一个任务的状态已变。</summary>
    void MarkDirty(TaskId id);

    /// <summary>刷新全部待写任务。</summary>
    Task FlushAsync();

    /// <summary>删除一个任务的存档。</summary>
    void Remove(TaskId id);
}

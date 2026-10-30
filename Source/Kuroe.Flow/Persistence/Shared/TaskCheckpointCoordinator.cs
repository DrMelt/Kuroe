using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Persistence.Shared;

/// <summary>任务检查点协调：接收变更标记，合并并写入检查点。</summary>
public interface ITaskCheckpointCoordinator : ITaskChangeSink
{
    /// <summary>刷新全部待写任务。</summary>
    Task FlushAsync();

    /// <summary>删除一个任务的检查点。</summary>
    void Remove(TaskId id);
}

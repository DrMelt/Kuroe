using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Persistence.Shared;

/// <summary>节点自身的检查点契约。节点只捕获与恢复自己的状态。</summary>
public interface INodeCheckpointSource
{
    /// <summary>捕获节点状态。没有状态的节点返回空。</summary>
    NodeCheckpoint? CaptureCheckpoint();

    /// <summary>从检查点恢复节点状态。已发布执行经上下文解析。</summary>
    void RestoreCheckpoint(NodeCheckpoint checkpoint, INodeRestoreContext context);
}

/// <summary>节点恢复时可用的执行解析。</summary>
public interface INodeRestoreContext
{
    /// <summary>按执行号取已恢复的执行，不存在时为空。</summary>
    IExecution? Execution(ExecutionId id);
}

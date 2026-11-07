using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Persistence.Records.Shared;

/// <summary>节点自身的持久记录契约。节点只捕获与恢复自己的状态。</summary>
public interface INodeRecordSource
{
    /// <summary>捕获节点状态。没有状态的节点返回空。</summary>
    NodeRecord? CaptureRecord();

    /// <summary>从持久记录恢复节点状态。已发布执行经上下文解析。</summary>
    void RestoreRecord(NodeRecord record, INodeRestoreContext context);
}

/// <summary>节点端口历史的持久记录契约。</summary>
public interface IPortRecordSource
{
    /// <summary>捕获节点的全部端口发布历史。</summary>
    IReadOnlyList<PortPublicationRecord> CapturePorts();

    /// <summary>恢复节点的端口发布历史。</summary>
    void RestorePorts(IReadOnlyList<PortPublicationRecord> records);
}

/// <summary>节点恢复时可用的执行解析。</summary>
public interface INodeRestoreContext
{
    /// <summary>按执行号取已恢复的执行，不存在时为空。</summary>
    IExecution? Execution(ExecutionId id);
}

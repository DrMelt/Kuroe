using Kuroe.Flow.Tasks.Records.Shared;

namespace Kuroe.Flow.Control.Shared;

/// <summary>流程存档捕获：流程记录。要求持有任务 Gate。</summary>
public interface IFlowRecordSource
{
    /// <summary>捕获流程记录。</summary>
    FlowRecord CaptureRecord(ITaskFlowContext task);
}
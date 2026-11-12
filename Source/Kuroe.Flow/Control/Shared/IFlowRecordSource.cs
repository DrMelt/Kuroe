using Kuroe.Flow.Execution.Records;
using Kuroe.Flow.Tasks.Records;

namespace Kuroe.Flow.Control;

/// <summary>流程存档捕获：流程记录与执行记录。要求持有任务 Gate。</summary>
public interface IFlowRecordSource
{
    /// <summary>捕获流程记录。</summary>
    FlowRecord CaptureRecord(ITaskFlowContext task);

    /// <summary>捕获流程执行记录。</summary>
    IReadOnlyList<ExecutionRecord> CaptureExecutions(ITaskFlowContext task);
}
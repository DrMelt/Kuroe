using ErrorOr;

namespace Kuroe.Flow.Execution;

/// <summary>上报本轮无法完成的入口。工具实现依赖此契约，具体记录由流程控制实现。</summary>
public interface IExecutionCannotCompleteSubmitter
{
    /// <summary>上报本轮无法完成并给出原因，成功时给出上报后的本轮契约状态。</summary>
    ErrorOr<ExecutionStatus> Report(ExecutionId id, string reason);
}
using ErrorOr;

namespace Kuroe.Execution.Shared;

/// <summary>本轮执行契约状态的读取面：按执行号取缺项与已上报的无法完成原因。</summary>
public interface IExecutionStatusReader
{
    /// <summary>按执行号读出本轮契约状态。</summary>
    ErrorOr<ExecutionStatus> Read(ExecutionId id);
}
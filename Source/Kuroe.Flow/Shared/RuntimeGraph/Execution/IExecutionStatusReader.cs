using ErrorOr;

namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>本轮契约状态的读取面：执行侧实现按它取本轮的缺项与已上报的无法完成原因。</summary>
public interface IExecutionStatusReader
{
    /// <summary>按执行号读出本轮契约状态，执行不存在或没有归属任务时返回错误。</summary>
    ErrorOr<ExecutionStatus> Read(ExecutionId id);
}
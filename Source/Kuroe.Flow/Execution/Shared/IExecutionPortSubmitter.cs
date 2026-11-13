using ErrorOr;

namespace Kuroe.Flow.Execution.Shared;

/// <summary>交回命名端口产出的入口。工具实现依赖此契约，具体提交由执行契约写入口完成。</summary>
public interface IExecutionPortSubmitter
{
    /// <summary>交回端口产出，成功时给出提交后的本轮契约状态。</summary>
    ErrorOr<ExecutionStatus> Submit(ExecutionId id, IReadOnlyList<ExecutionOutputValue> values);
}
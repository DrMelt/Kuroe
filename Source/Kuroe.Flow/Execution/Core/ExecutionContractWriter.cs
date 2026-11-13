using Kuroe.Flow.Execution.Shared;

namespace Kuroe.Flow.Execution;

/// <summary>执行契约的写入口：把端口产出与无法完成原因写到目标执行。只依赖执行实例，不持有执行运行期。</summary>
public sealed class ExecutionContractWriter : IExecutionContractWriter
{
    /// <inheritdoc/>
    public void SubmitOutputs(
        IExecution execution,
        IReadOnlyDictionary<ExecutionPortName, ExecutionOutputValue> outputs) =>
        Unit(execution).SubmitOutputs(outputs);

    /// <inheritdoc/>
    public void ReportCannotComplete(IExecution execution, string reason) =>
        Unit(execution).ReportCannotComplete(reason);

    private static Execution Unit(IExecution execution) => (Execution)execution;
}
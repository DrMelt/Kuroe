using ErrorOr;
using Kuroe.Execution.Shared.Contract;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Ports;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Execution;

/// <summary>一次执行的请求入口：把运行器与工具的请求交给执行实例处理。</summary>
internal sealed class ExecutionRequestEntry(ExecutionInstance execution) : IExecutionRequestEntry
{
    /// <inheritdoc/>
    public ExecutionId Id => execution.Id;

    /// <inheritdoc/>
    public ExecutionRequest Request => execution.Request;

    /// <inheritdoc/>
    public ExecutionStatus ReadStatus() => execution.ReadStatus();

    /// <inheritdoc/>
    public ErrorOr<ExecutionStatus> SubmitOutputs(IReadOnlyList<ExecutionOutputValue> values) =>
        execution.SubmitOutputs(values);

    /// <inheritdoc/>
    public ErrorOr<ExecutionStatus> ReportCannotComplete(string reason) =>
        execution.ReportCannotComplete(reason);
}
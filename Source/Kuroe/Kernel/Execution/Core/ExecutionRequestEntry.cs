using ErrorOr;
using Kuroe.Kernel.Execution.Shared.Contract;
using Kuroe.Kernel.Execution.Shared.Identity;
using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Runtime;

namespace Kuroe.Kernel.Execution;

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
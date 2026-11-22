using Kuroe.Kernel.Input.Shared;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Runtime;

namespace Kuroe.Runner.Approval;

/// <summary>为审批执行建立运行器。</summary>
public sealed class ApprovalExecutionRunnerFactory(
    IInputChannel channel,
    TimeProvider time) : IExecutionRunnerFactory
{
    /// <inheritdoc/>
    public IExecutionRunner Create(
        IExecutionParameters parameters,
        IExecutionRequestEntry requests) =>
        parameters is ApprovalExecutionParameters approval
            ? new ApprovalExecutionRunner(channel, approval, requests, time)
            : throw new InvalidOperationException("审批执行参数类型不匹配。");
}
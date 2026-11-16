using Kuroe.Execution.Shared;
using Kuroe.Input.Shared;

namespace Kuroe.Runner.Approval;

/// <summary>为审批执行建立运行器。</summary>
public sealed class ApprovalExecutionRunnerFactory(
    ExecutionKind kind,
    IInputChannel channel) : IExecutionRunnerFactory
{
    /// <inheritdoc/>
    public ExecutionKind Kind => kind;

    /// <inheritdoc/>
    public IExecutionRunner Create(IExecutionTarget target, ExecutionId execution) =>
        target.Parameters is ApprovalExecutionParameters parameters
            ? new ApprovalExecutionRunner(channel, parameters)
            : throw new InvalidOperationException("审批执行参数类型不匹配。");
}
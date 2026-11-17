using Kuroe.Input.Shared;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Runner.Approval;

/// <summary>为审批执行建立运行器。</summary>
public sealed class ApprovalExecutionRunnerFactory(
    IInputChannel channel) : IExecutionRunnerFactory
{
    /// <summary>本工厂的注册键。</summary>
    public static ExecutionRunnerKey Key { get; } = new("Approval");

    /// <inheritdoc/>
    ExecutionRunnerKey IExecutionRunnerFactory.Key => Key;

    /// <inheritdoc/>
    public IExecutionRunner Create(IExecutionTarget target, ExecutionId execution) =>
        target.Parameters is ApprovalExecutionParameters parameters
            ? new ApprovalExecutionRunner(channel, parameters)
            : throw new InvalidOperationException("审批执行参数类型不匹配。");
}
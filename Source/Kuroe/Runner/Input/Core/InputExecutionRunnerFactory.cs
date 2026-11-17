using Kuroe.Input.Shared;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Runner.Input;

/// <summary>为输入执行建立运行器。</summary>
public sealed class InputExecutionRunnerFactory(
    IInputChannel channel) : IExecutionRunnerFactory
{
    /// <summary>本工厂的注册键。</summary>
    public static ExecutionRunnerKey Key { get; } = new("Input");

    /// <inheritdoc/>
    ExecutionRunnerKey IExecutionRunnerFactory.Key => Key;

    /// <inheritdoc/>
    public IExecutionRunner Create(IExecutionTarget target, ExecutionId execution) =>
        target.Parameters is InputExecutionParameters parameters
            ? new InputExecutionRunner(channel, parameters)
            : throw new InvalidOperationException("输入执行参数类型不匹配。");
}
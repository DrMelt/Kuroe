using Kuroe.Execution.Shared;
using Kuroe.Input.Shared;

namespace Kuroe.Runner.Input;

/// <summary>为输入执行建立运行器。</summary>
public sealed class InputExecutionRunnerFactory(
    IInputChannel channel) : IExecutionRunnerFactory
{
    /// <inheritdoc/>
    public IExecutionRunner Create(IExecutionTarget target, ExecutionId execution) =>
        target.Parameters is InputExecutionParameters parameters
            ? new InputExecutionRunner(channel, parameters)
            : throw new InvalidOperationException("输入执行参数类型不匹配。");
}
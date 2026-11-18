using Kuroe.Input.Shared;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Runner.Input;

/// <summary>为输入执行建立运行器。</summary>
public sealed class InputExecutionRunnerFactory(
    IInputChannel channel) : IExecutionRunnerFactory
{
    /// <inheritdoc/>
    public IExecutionRunner Create(
        IExecutionParameters parameters,
        IExecutionRequestEntry requests) =>
        parameters is InputExecutionParameters input
            ? new InputExecutionRunner(channel, input, requests)
            : throw new InvalidOperationException("输入执行参数类型不匹配。");
}
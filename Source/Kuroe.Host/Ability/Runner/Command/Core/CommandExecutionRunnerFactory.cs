using Kuroe.Process;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Runtime;

namespace Kuroe.Host.Ability.Runner.Command;

/// <summary>为命令执行建立运行器。</summary>
public sealed class CommandExecutionRunnerFactory(
    IProcessRunner process,
    TimeProvider time) : IExecutionRunnerFactory
{
    /// <inheritdoc/>
    public IExecutionRunner Create(
        IExecutionParameters parameters,
        IExecutionRequestEntry requests) =>
        parameters is CommandExecutionParameters command
            ? new CommandExecutionRunner(command, process, requests, time)
            : throw new InvalidOperationException("命令执行参数类型不匹配。");
}
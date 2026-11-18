using Kuroe.Process;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Runner.Command;

/// <summary>为命令执行建立运行器。</summary>
public sealed class CommandExecutionRunnerFactory(
    IProcessRunner process) : IExecutionRunnerFactory
{
    /// <inheritdoc/>
    public IExecutionRunner Create(
        IExecutionParameters parameters,
        IExecutionRequestEntry requests) =>
        parameters is CommandExecutionParameters command
            ? new CommandExecutionRunner(command, process, requests)
            : throw new InvalidOperationException("命令执行参数类型不匹配。");
}
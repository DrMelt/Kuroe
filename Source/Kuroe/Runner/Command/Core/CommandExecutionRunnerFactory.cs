using Kuroe.Process;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Runner.Command;

/// <summary>为命令执行建立运行器。</summary>
public sealed class CommandExecutionRunnerFactory(
    IProcessRunner process) : IExecutionRunnerFactory
{
    /// <summary>本工厂的注册键。</summary>
    public static ExecutionRunnerKey Key { get; } = new("Command");

    /// <inheritdoc/>
    ExecutionRunnerKey IExecutionRunnerFactory.Key => Key;

    /// <inheritdoc/>
    public IExecutionRunner Create(IExecutionParameters parameters, ExecutionId execution) =>
        parameters is CommandExecutionParameters command
            ? new CommandExecutionRunner(command, process)
            : throw new InvalidOperationException("命令执行参数类型不匹配。");
}
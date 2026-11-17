using Kuroe.Process;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Runner.Command;

/// <summary>为命令执行建立运行器。</summary>
public sealed class CommandExecutionRunnerFactory(
    IProcessRunner process) : IExecutionRunnerFactory
{
    /// <inheritdoc/>
    public IExecutionRunner Create(IExecutionTarget target, ExecutionId execution) =>
        target.Parameters is CommandExecutionParameters parameters
            ? new CommandExecutionRunner(parameters, process)
            : throw new InvalidOperationException("命令执行参数类型不匹配。");
}
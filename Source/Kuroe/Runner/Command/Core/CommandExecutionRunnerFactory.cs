using Kuroe.Execution.Shared;
using Kuroe.Process;

namespace Kuroe.Runner.Command;

/// <summary>为命令执行建立运行器。</summary>
public sealed class CommandExecutionRunnerFactory(
    ExecutionKind kind,
    IProcessRunner process) : IExecutionRunnerFactory
{
    /// <inheritdoc/>
    public ExecutionKind Kind => kind;

    /// <inheritdoc/>
    public IExecutionRunner Create(IExecutionTarget target, ExecutionId execution) =>
        target.Parameters is CommandExecutionParameters parameters
            ? new CommandExecutionRunner(parameters, process)
            : throw new InvalidOperationException("命令执行参数类型不匹配。");
}
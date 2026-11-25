using ErrorOr;
using Kuroe.Kernel.Execution.Shared.Request;

namespace Kuroe.Kernel.Execution.Shared.Runtime;

/// <summary>执行器绑定的通用实现：固定运行器工厂，参数由给定构造给出。</summary>
public sealed class ExecutionBinder(
    IExecutionRunnerFactory runnerFactory,
    Func<ExecutionParameterContext, ErrorOr<IExecutionParameters>> parameters) : IExecutionBinder
{
    /// <inheritdoc/>
    public IExecutionRunnerFactory RunnerFactory { get; } = runnerFactory;

    /// <inheritdoc/>
    public ErrorOr<IExecutionParameters> Parameters(ExecutionParameterContext context) => parameters(context);
}

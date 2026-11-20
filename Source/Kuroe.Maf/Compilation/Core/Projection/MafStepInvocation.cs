using ErrorOr;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Runtime;
using Kuroe.Maf.Definition;

namespace Kuroe.Maf.Compilation.Projection;

/// <summary>执行节点的 MAF 步骤装配：由执行器绑定给出运行器工厂与参数。</summary>
internal sealed class MafStepInvocation(IExecutionBinder binder) : IMafStepInvocationFactory
{
    /// <inheritdoc/>
    public ErrorOr<ExecutionInvocation> Create(
        MafStepId step,
        string model,
        ExecutionParameterContext context)
    {
        ErrorOr<IExecutionParameters> parameters = binder.Parameters(context with { Model = model });

        return parameters.IsError
            ? parameters.ErrorsOrEmptyList
            : new ExecutionInvocation(step.ToExecutionStepId(), binder.RunnerFactory, parameters.Value);
    }
}

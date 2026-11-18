using ErrorOr;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Maf.Definition;

/// <summary>MAF 步骤的执行装配工厂：按步骤标识、模型与本次执行上下文建立执行装配结果。</summary>
public interface IMafStepInvocationFactory
{
    /// <summary>建立步骤的执行装配结果。装配结果的步骤标识必须与步骤标识一致。</summary>
    ErrorOr<ExecutionInvocation> Create(
        MafStepId step,
        string model,
        ExecutionParameterContext context);
}

/// <summary>MAF 步骤的执行装配声明：执行运行器工厂与按本次执行上下文构造的参数。</summary>
public sealed record MafStepInvocationFactory(
    IExecutionRunnerFactory Factory,
    Func<ExecutionParameterContext, ErrorOr<IExecutionParameters>> Parameters) : IMafStepInvocationFactory
{
    /// <inheritdoc/>
    public ErrorOr<ExecutionInvocation> Create(
        MafStepId step,
        string model,
        ExecutionParameterContext context)
    {
        ErrorOr<IExecutionParameters> parameters = Parameters(context with { Model = model });
        if (parameters.IsError)
        {
            return parameters.ErrorsOrEmptyList;
        }

        return new ExecutionInvocation(step.ToExecutionStepId(), Factory, parameters.Value);
    }
}
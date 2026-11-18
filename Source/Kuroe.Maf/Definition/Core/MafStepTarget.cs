using ErrorOr;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Maf.Definition;

/// <summary>MAF 步骤的执行目标工厂：按步骤标识、模型与本次执行上下文建立执行目标。</summary>
public interface IMafStepTargetFactory
{
    /// <summary>建立步骤的执行目标。目标标识必须与步骤标识一致。</summary>
    ErrorOr<ExecutionInvocation> Create(
        MafStepId step,
        string model,
        ExecutionParameterContext context);
}

/// <summary>MAF 步骤的执行目标声明：执行运行器工厂与按本次执行上下文构造的参数。</summary>
public sealed record MafStepTarget(
    IExecutionRunnerFactory Factory,
    Func<ExecutionParameterContext, ErrorOr<IExecutionParameters>> Parameters) : IMafStepTargetFactory
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

        return new ExecutionInvocation(step.ToExecutionTargetId(), Factory, parameters.Value);
    }
}
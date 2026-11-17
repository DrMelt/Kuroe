using ErrorOr;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Maf.Definition;

/// <summary>MAF 步骤的执行目标工厂：按步骤标识与模型建立执行目标。提交与恢复时各调用一次，返回的目标由步骤的多次运行共用。</summary>
public interface IMafStepTargetFactory
{
    /// <summary>建立步骤的执行目标。目标标识必须与步骤标识一致。</summary>
    ErrorOr<IExecutionTarget> Create(MafStepId step, string model);
}

/// <summary>MAF 步骤的执行目标声明：执行运行器键与按模型构造的参数。参数在提交或恢复时构造一次，由步骤的多次运行共用。</summary>
public sealed record MafStepTarget(
    ExecutionRunnerKey RunnerKey,
    Func<string, ErrorOr<IExecutionParameters>> Parameters) : IMafStepTargetFactory
{
    /// <inheritdoc/>
    public ErrorOr<IExecutionTarget> Create(MafStepId step, string model)
    {
        ErrorOr<IExecutionParameters> parameters = Parameters(model);
        if (parameters.IsError)
        {
            return parameters.ErrorsOrEmptyList;
        }

        return new MafExecutionTarget(step.ToExecutionTargetId(), RunnerKey, parameters.Value);
    }
}

/// <summary>由框架构造的执行目标，标识来自步骤标识。</summary>
internal sealed record MafExecutionTarget(
    ExecutionTargetId Id,
    ExecutionRunnerKey RunnerKey,
    IExecutionParameters Parameters) : IExecutionTarget;
using ErrorOr;
using Kuroe.Flow.Control;
using Kuroe.Flow.Execution;
using ExecutionStatus = Kuroe.Flow.Execution.ExecutionStatus;

namespace Kuroe.Flow.Control.Contract;

/// <summary>模型侧交回结构化产出的入口：输出端口交端口名到端口值，Text 端口给内容，Signal 端口只给端口名。
/// 提交者身份在此认定，结果以提交后的本轮契约状态交回，回执措辞由工具实现层给出。</summary>
public sealed class PortSubmitter(
    IExecutionOwnerIndex executions,
    IExecutionContractWriter contracts) : IExecutionPortSubmitter
{
    /// <summary>需交回输出端口的执行交回端口产出，端口必须与需交回的端口一一对应。</summary>
    public ErrorOr<ExecutionStatus> Submit(
        ExecutionId id,
        IReadOnlyList<ExecutionOutputValue> values)
    {
        ErrorOr<(IExecution Execution, ITaskFlowContext Task)> owner = SubmitterContext.Resolve(
            executions,
            id,
            "提交端口产出");
        if (owner.IsError)
        {
            return owner.ErrorsOrEmptyList;
        }

        if (values.Count == 0)
        {
            return [ContractErrors.Ports("端口产出为空")];
        }

        Dictionary<ExecutionPortName, ExecutionOutputValue> submitted = [];
        foreach (ExecutionOutputValue value in values)
        {
            ErrorOr<ExecutionPortName> name = ExecutionPortName.Create(value.Port.Value);
            if (name.IsError)
            {
                return name.ErrorsOrEmptyList;
            }

            submitted[name.Value] = value with { Port = name.Value };
        }

        (IExecution execution, ITaskFlowContext task) = owner.Value;
        lock (task.Gate)
        {
            if (execution.Contract.Outputs is not null)
            {
                return Error.Validation("Tool.Argument", "端口产出已提交，无需重复提交。");
            }

            ErrorOr<Success> valid = ExecutionPortRules.Validate(execution.Request, submitted);
            if (valid.IsError)
            {
                return valid.ErrorsOrEmptyList;
            }

            ExecutionContract contract = new(submitted, execution.Contract.CannotCompleteReason);
            ErrorOr<Success> validContract = ExecutionContractRules.Validate(execution.Request, contract);
            if (validContract.IsError)
            {
                return validContract.ErrorsOrEmptyList;
            }

            contracts.SubmitOutputs(execution, submitted);

            return ExecutionStatusReader.Of(execution);
        }
    }
}
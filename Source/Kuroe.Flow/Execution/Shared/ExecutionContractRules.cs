using ErrorOr;

namespace Kuroe.Flow.Execution.Shared;

/// <summary>执行契约的端口级校验：未声明端口、端口键不一致、必填缺失与空白原因。</summary>
public static class ExecutionContractRules
{
    /// <summary>按执行请求校验契约。未声明端口、键不一致、缺少必填端口或原因为空白时返回错误。</summary>
    public static ErrorOr<Success> Validate(ExecutionRequest request, ExecutionContract contract)
    {
        IReadOnlyDictionary<ExecutionPortName, ExecutionOutputValue> outputs =
            contract.Outputs ?? new Dictionary<ExecutionPortName, ExecutionOutputValue>();
        Dictionary<ExecutionPortName, ExecutionOutputPort> declared = request.OutputPorts
            .ToDictionary(port => port.Name);

        foreach ((ExecutionPortName port, ExecutionOutputValue value) in outputs)
        {
            if (!declared.ContainsKey(port))
            {
                return [Error.Validation(ExecutionErrorCodes.Contract, $"执行交回了未声明的端口 {port}。")];
            }

            if (value.Port != port)
            {
                return [Error.Validation(ExecutionErrorCodes.Contract, $"执行产出端口 {value.Port} 与契约键 {port} 不一致。")];
            }
        }

        if (contract.CannotCompleteReason is { } reason && string.IsNullOrWhiteSpace(reason))
        {
            return [Error.Validation(ExecutionErrorCodes.Contract, "无法完成原因不能为空白。")];
        }

        if (!string.IsNullOrWhiteSpace(contract.CannotCompleteReason))
        {
            return Result.Success;
        }

        foreach (ExecutionOutputPort port in request.OutputPorts)
        {
            if (port.Required && !outputs.ContainsKey(port.Name))
            {
                return [Error.Validation(ExecutionErrorCodes.Contract, $"执行缺少必填端口 {port.Name}。")];
            }
        }

        return Result.Success;
    }
}
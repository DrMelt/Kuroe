using ErrorOr;
using Kuroe.Kernel.Execution.Shared.Errors;
using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Execution.Shared.Request;

namespace Kuroe.Kernel.Execution.Shared.Contract;

/// <summary>执行契约的端口整理与校验：端口一一对应、触发位规则、未声明端口、必填缺失与空白原因。</summary>
public static class ExecutionContractRules
{
    /// <summary>校验交回的端口与声明需交回的端口一一对应，并整理为契约输出。</summary>
    public static ErrorOr<IReadOnlyDictionary<ExecutionPortName, ExecutionOutputValue>> PrepareOutputs(
        ExecutionRequest request,
        IReadOnlyList<ExecutionOutputValue> values)
    {
        if (values.Count == 0)
        {
            return [Error.Validation(ExecutionErrorCodes.Contract, "端口产出为空。")];
        }

        Dictionary<ExecutionPortName, ExecutionOutputValue> submitted = [];
        foreach (ExecutionOutputValue value in values)
        {
            ErrorOr<ExecutionPortName> name = ExecutionPortName.Create(value.Port.Value);
            if (name.IsError)
            {
                return name.ErrorsOrEmptyList;
            }

            if (!submitted.TryAdd(name.Value, value with { Port = name.Value }))
            {
                return [Error.Validation(ExecutionErrorCodes.Contract, $"端口 {name.Value} 重复提交。")];
            }
        }

        IReadOnlyList<ExecutionOutputPort> required = [.. request.OutputPorts.Where(port => port.Required)];
        if (required.Count == 0)
        {
            return [Error.Validation(ExecutionErrorCodes.Contract, "该执行没有需要交回的输出端口。")];
        }

        IReadOnlyList<ExecutionPortName> expected = [.. required.Select(port => port.Name)];
        if (!expected.ToHashSet().SetEquals(submitted.Keys))
        {
            return [Error.Validation(
                ExecutionErrorCodes.Contract,
                $"端口必须与需交回的端口一一对应，应提交 {string.Join('、', expected)}。")];
        }

        foreach (ExecutionOutputPort port in required)
        {
            if (!port.CarriesTrigger
                && submitted.TryGetValue(port.Name, out ExecutionOutputValue? value)
                && !value.Fires)
            {
                return [Error.Validation(
                    ExecutionErrorCodes.Contract,
                    $"端口 {port.Name} 不承载触发，必须驱动下游，不能给出不触发值。")];
            }
        }

        return submitted;
    }

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
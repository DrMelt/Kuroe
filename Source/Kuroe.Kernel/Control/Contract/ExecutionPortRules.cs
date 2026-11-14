using ErrorOr;
using Kuroe.Execution.Shared;

namespace Kuroe.Control.Contract;

/// <summary>端口交回的纯校验：端口集合、文本产出与触发位，不读取执行状态。</summary>
internal static class ExecutionPortRules
{
    /// <summary>校验交回的端口是否与声明需交回的端口一一对应，不承载触发的端口不能给出不触发值。</summary>
    public static ErrorOr<Success> Validate(
        ExecutionRequest request,
        IReadOnlyDictionary<ExecutionPortName, ExecutionOutputValue> values)
    {
        if (values.Count == 0)
        {
            return [ContractErrors.Ports("端口产出为空")];
        }

        IReadOnlyList<ExecutionOutputPort> required = [.. request.OutputPorts.Where(port => port.Required)];
        if (required.Count == 0)
        {
            return Error.Validation("Tool.Argument", "该执行没有需要交回的输出端口。");
        }

        HashSet<ExecutionPortName> expected = [.. required.Select(port => port.Name)];
        if (!expected.SetEquals(values.Keys))
        {
            return Error.Validation("Tool.Argument", $"端口必须与需交回的端口一一对应，应提交 {string.Join('、', expected)}。");
        }

        if (TriggerOnSilentPort(required, values) is { } silent)
        {
            return [ContractErrors.Ports($"端口 {silent} 不承载触发值，不能给出触发位")];
        }

        return Result.Success;
    }

    /// <summary>不承载触发的端口给出不触发值时命中的端口名，按声明序取第一个。</summary>
    private static ExecutionPortName? TriggerOnSilentPort(
        IReadOnlyList<ExecutionOutputPort> declared,
        IReadOnlyDictionary<ExecutionPortName, ExecutionOutputValue> values) =>
        declared
            .Where(port => !port.CarriesTrigger
                && values.TryGetValue(port.Name, out ExecutionOutputValue? value)
                && !value.Fires)
            .Select(port => (ExecutionPortName?)port.Name)
            .FirstOrDefault();
}
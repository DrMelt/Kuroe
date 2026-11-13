using ErrorOr;
using Kuroe.Flow.Execution.Shared;

namespace Kuroe.Flow.Control.Contract;

/// <summary>端口交回的纯校验：端口集合、文本产出与触发位，不读取执行状态。</summary>
internal static class ExecutionPortRules
{
    /// <summary>校验交回的端口是否与声明需交回的端口一一对应，文本端口必须有内容，不承载触发的端口不能给出不触发值。</summary>
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

        if (BlankTextPort(required, values) is { } blank)
        {
            return [ContractErrors.Ports($"端口 {blank} 的产出为空")];
        }

        if (TriggerOnSilentPort(required, values) is { } silent)
        {
            return [ContractErrors.Ports($"端口 {silent} 不承载触发值，不能给出触发位")];
        }

        return Result.Success;
    }

    /// <summary>交回空文本的 Text 语义端口名，按声明序取第一个；Signal 端口不承载内容，给出的取值不作产出。</summary>
    private static ExecutionPortName? BlankTextPort(
        IReadOnlyList<ExecutionOutputPort> declared,
        IReadOnlyDictionary<ExecutionPortName, ExecutionOutputValue> values) =>
        declared
            .Where(port => port.Kind == ExecutionPortKind.Text
                && values.TryGetValue(port.Name, out ExecutionOutputValue? value)
                && string.IsNullOrWhiteSpace(value.Text))
            .Select(port => (ExecutionPortName?)port.Name)
            .FirstOrDefault();

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
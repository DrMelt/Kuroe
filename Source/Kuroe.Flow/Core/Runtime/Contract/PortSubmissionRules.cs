using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Runtime.Contract;

/// <summary>端口交回的纯校验：端口集合、文本产出与触发位，不读取执行状态。</summary>
internal static class PortSubmissionRules
{
    /// <summary>校验交回的端口是否与声明的需交回端口一一对应，文本端口必须有内容，不承载触发的端口不能给出不触发值。</summary>
    public static ErrorOr<Success> Validate(
        IReadOnlyList<Port> declared,
        IReadOnlyDictionary<PortName, PortValue> values)
    {
        if (values.Count == 0)
        {
            return [ContractErrors.Ports("端口产出为空")];
        }

        if (declared.Count == 0)
        {
            return Error.Validation("Tool.Argument", "该节点没有需要交回的输出端口。");
        }

        HashSet<PortName> expected = [.. declared.Select(port => port.Name)];
        if (!expected.SetEquals(values.Keys))
        {
            return Error.Validation("Tool.Argument", $"端口必须与需交回的端口一一对应，应提交 {string.Join('、', expected)}。");
        }

        if (BlankTextPort(declared, values) is { } blank)
        {
            return [ContractErrors.Ports($"端口 {blank} 的产出为空")];
        }

        if (TriggerOnSilentPort(declared, values) is { } silent)
        {
            return [ContractErrors.Ports($"端口 {silent} 不承载触发值，不能给出触发位")];
        }

        return Result.Success;
    }

    /// <summary>交回空文本的 Text 语义端口名，按声明序取第一个；Signal 端口不承载内容，给出的取值不作产出。</summary>
    private static PortName? BlankTextPort(
        IReadOnlyList<Port> declared,
        IReadOnlyDictionary<PortName, PortValue> values) =>
        declared
            .Where(port => port.Type.Semantics == PortSemantics.Text
                && string.IsNullOrWhiteSpace(values.GetValueOrDefault(port.Name).Text))
            .Select(port => (PortName?)port.Name)
            .FirstOrDefault();

    /// <summary>不承载触发的端口给出不触发值时命中的端口名，按声明序取第一个。</summary>
    private static PortName? TriggerOnSilentPort(
        IReadOnlyList<Port> declared,
        IReadOnlyDictionary<PortName, PortValue> values) =>
        declared
            .Where(port => !port.Trigger && !values.GetValueOrDefault(port.Name).Fires)
            .Select(port => (PortName?)port.Name)
            .FirstOrDefault();
}

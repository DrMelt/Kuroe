using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Runtime.Contract;

/// <summary>端口交回的纯校验：端口集合与触发位，不读取执行状态。</summary>
internal static class PortSubmissionRules
{
    /// <summary>校验交回的端口是否与声明的需交回端口一一对应，不承载触发的端口不能给出触发位，承载触发的端口必须给出触发位。</summary>
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
            return Error.Validation("Tool.Argument", $"端口必须与需交回的端口一一对应，应提交 {PortRules.Names(declared)}。");
        }

        if (TriggerOnSilentPort(declared, values) is { } silent)
        {
            return [ContractErrors.Ports($"端口 {silent} 不承载触发值，不能给出触发位")];
        }

        if (MissingTrigger(declared, values) is { } missing)
        {
            return [ContractErrors.Ports($"端口 {missing} 必须给出触发位")];
        }

        return Result.Success;
    }

    /// <summary>不承载触发的端口给出触发位时命中的端口名，按声明序取第一个。</summary>
    private static PortName? TriggerOnSilentPort(
        IReadOnlyList<Port> declared,
        IReadOnlyDictionary<PortName, PortValue> values) =>
        declared
            .Where(port => !port.SubmissionShape().IncludesTrigger() && values.GetValueOrDefault(port.Name).Trigger is not null)
            .Select(port => (PortName?)port.Name)
            .FirstOrDefault();

    /// <summary>触发端口未给出触发位时命中的端口名，按声明序取第一个。</summary>
    private static PortName? MissingTrigger(
        IReadOnlyList<Port> declared,
        IReadOnlyDictionary<PortName, PortValue> values) =>
        declared
            .Where(port => port.SubmissionShape().IncludesTrigger() && values.GetValueOrDefault(port.Name).Trigger is null)
            .Select(port => (PortName?)port.Name)
            .FirstOrDefault();
}

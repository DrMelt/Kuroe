using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;

namespace Kuroe.Flow.Shared.StaticGraph.Ports;

/// <summary>端口与上游连接的展示名。</summary>
public static class PortLabels
{
    /// <summary>输出端口的展示：普通 Text 端口只写名字，其余标出类型与模式；固定值与引用来源附后，说明存在时写在末尾。</summary>
    public static string Label(this Port port)
    {
        string text = Format(port.Name, port.Type, port.Mode, port.Description);
        if (port.Value is { Length: > 0 } value)
        {
            text = $"{text}={value}";
        }
        else if (port.Ref is { } reference)
        {
            text = $"{text}←{reference.Display}";
        }

        return text;
    }

    /// <summary>输入端口的展示：普通 Text 端口只写名字，其余标出类型与模式，说明存在时写在末尾，默认值附后。</summary>
    public static string Label(this InputPort port)
    {
        string text = Format(port.Name, port.Type, port.Mode, port.Description);
        if (port.Default is { Length: > 0 } value)
        {
            text = $"{text}，默认 {value}";
        }

        return text;
    }

    /// <summary>一条上游连接的展示：输入名与来源，翻转条件标出。</summary>
    public static string Label(this FromLink link) => $"{link.Input} ← {link.SourceLabel()}";

    /// <summary>一组 From 连接的展示：组内连接按序排列。</summary>
    public static string Label(this FromGroup group) =>
        string.Join("、", group.Links.Select(link => link.Label()));

    /// <summary>连接来源的展示：翻转条件附后。</summary>
    public static string SourceLabel(this FromLink link) =>
        link.Condition == TriggerCondition.Negative ? $"{link.Ref.Display}·翻转" : link.Ref.Display;

    /// <summary>开始端口的展示：带固定值时写出端口与值，带默认值时写出默认值，由宿主给值时附宿主输入名，常量端口标出常量。</summary>
    public static string StartLabel(
        this Port port,
        IReadOnlyDictionary<PortName, string>? hostInputs = null)
    {
        string text = port.Name.Value;
        if (port.Value is { Length: > 0 } value)
        {
            text = $"{port.Name.Value}={value}";
        }
        else if (port.Default is { Length: > 0 } defaultValue)
        {
            text = $"{port.Name.Value}，默认 {defaultValue}";
        }

        if (hostInputs is not null && hostInputs.TryGetValue(port.Name, out string? input))
        {
            text = $"{text}←{input}";
        }

        return port.IsConstant ? $"{text}·常量" : text;
    }

    /// <summary>静态端口绑定来源的展示：成员路径@端口。直接绑定开始端口且该端口有宿主输入名时附上输入名。</summary>
    public static string StaticLabel(
        this OutputBinding binding,
        IReadOnlyDictionary<PortName, string>? hostInputs = null)
    {
        string text = $"{binding.Source}@{binding.Port.Value}";
        if (hostInputs is not null
            && binding.Source.Depth == 1
            && binding.Source.Label == StartNode.ReservedLabel
            && hostInputs.TryGetValue(binding.Port, out string? input))
        {
            text = $"{text}←{input}";
        }

        return text;
    }

    /// <summary>执行节点静态端口的展示：有绑定给绑定来源，没有绑定时给默认值，都没有时为空。</summary>
    public static string? StaticLabel(
        this ExecutableNode executable,
        PortName port,
        IReadOnlyDictionary<PortName, string>? hostInputs = null)
    {
        if (executable.InputBindings?.TryGetValue(port, out InputBinding? binding) == true
            && binding is { SourcePort: { } sourcePort })
        {
            return new OutputBinding(binding.Source, sourcePort).StaticLabel(hostInputs);
        }

        return executable.FindInput(port) is { IsStatic: true, Default: { Length: > 0 } value } ? value : null;
    }

    /// <summary>端口的展示：普通 Text 端口只写名字，非 Text 标出类型，常量与 Text 触发标出模式，说明存在时写在末尾。</summary>
    private static string Format(PortName name, PortType type, PortMode mode, string? description)
    {
        string label = type == PortTypes.Text ? name.Value : $"{name.Value}·{type}";
        if (mode == PortMode.Constant)
        {
            label = $"{label}·常量";
        }
        else if (mode == PortMode.Trigger && type == PortTypes.Text)
        {
            label = $"{label}·触发";
        }

        return description is { Length: > 0 } text ? $"{label}：{text}" : label;
    }
}

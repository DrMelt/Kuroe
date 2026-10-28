using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Presentation;

/// <summary>端口与上游连接的展示名。</summary>
public static class PortLabels
{
    /// <summary>输出端口的展示：Text 端口只写名字，其余标出类型，说明存在时写在末尾。</summary>
    public static string Label(this Port port) => Format(port.Name, port.Type, port.Description);

    /// <summary>输入端口的展示：Text 端口只写名字，其余标出类型，说明存在时写在末尾，默认值附后，静态端口标出静态。</summary>
    public static string Label(this InputPort port)
    {
        string text = Format(port.Name, port.Type, port.Description);
        if (port.Default is { Length: > 0 } value)
        {
            text = $"{text}，默认 {value}";
        }

        return port.IsStatic ? $"{text}·静态" : text;
    }

    /// <summary>一条上游连接的展示：输入名与来源端口。</summary>
    public static string Label(this FromLink link) => $"{link.Input} ← {link.Ref.Display}";

    /// <summary>一组启动条件的展示：组内连接按序排列。</summary>
    public static string Label(this FromGroup group) =>
        string.Join("、", group.Links.Select(link => link.Label()));

    /// <summary>开始端口的展示：带固定值时写出端口与值，绑定宿主输入时附宿主输入名，静态端口标出静态。</summary>
    public static string StartLabel(
        this Port port,
        IReadOnlyDictionary<PortName, string>? hostInputs = null)
    {
        string text = port.Value is { Length: > 0 } value ? $"{port.Name.Value}={value}" : port.Name.Value;
        if (hostInputs is not null && hostInputs.TryGetValue(port.Name, out string? input))
        {
            text = $"{text}←{input}";
        }

        return port.IsStatic ? $"{text}·静态" : text;
    }

    /// <summary>静态值来源的展示：常量给值；宿主来源在任务内给实际值，流程视图给宿主输入名。</summary>
    public static string StaticLabel(
        this StaticValue value,
        HostStartValues? hostValues = null,
        IReadOnlyDictionary<PortName, string>? hostInputs = null)
    {
        string? text = value.Text ?? (value.HostPort is { } port ? hostValues?.Text(port) : null);
        if (text is { Length: > 0 })
        {
            return text;
        }

        if (value.HostPort is not { } hostPort)
        {
            return "未绑定";
        }

        return hostInputs is not null && hostInputs.TryGetValue(hostPort, out string? input)
            ? $"宿主 {input}"
            : "宿主";
    }

    /// <summary>端口的展示：普通 Text 端口只写名字，其余标出类型，说明存在时写在末尾。</summary>
    private static string Format(PortName name, PortType type, string? description)
    {
        string label = type == PortTypes.Text ? name.Value : $"{name.Value}·{type}";

        return description is { Length: > 0 } text ? $"{label}：{text}" : label;
    }
}

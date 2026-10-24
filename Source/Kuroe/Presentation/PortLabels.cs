using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Presentation;

/// <summary>端口与上游连接的展示名。</summary>
public static class PortLabels
{
    /// <summary>输出端口的展示：Text 端口只写名字，其余标出类型，说明存在时写在末尾。</summary>
    public static string Label(this Port port) => Format(port.Name, port.Type, port.Description);

    /// <summary>输入端口的展示：Text 端口只写名字，其余标出类型，说明存在时写在末尾，默认值附后。</summary>
    public static string Label(this InputPort port) =>
        port.Default is { Length: > 0 } value
            ? $"{Format(port.Name, port.Type, port.Description)}，默认 {value}"
            : Format(port.Name, port.Type, port.Description);

    /// <summary>一条上游连接的展示：输入名与来源端口。</summary>
    public static string Label(this FromLink link) => $"{link.Input} ← {link.Ref.Display}";

    /// <summary>一组启动条件的展示：组内连接按序排列。</summary>
    public static string Label(this FromGroup group) =>
        string.Join("、", group.Links.Select(link => link.Label()));

    /// <summary>开始端口的展示：带固定值时写出端口与值。</summary>
    public static string StartLabel(this Port port) =>
        port.Value is { Length: > 0 } value ? $"{port.Name.Value}={value}" : port.Name.Value;

    /// <summary>端口的展示：Text 端口只写名字，其余标出类型，说明存在时写在末尾。</summary>
    private static string Format(PortName name, PortType type, string? description)
    {
        string label = type == PortType.Text ? name.Value : $"{name.Value}·{type}";

        return description is { Length: > 0 } text ? $"{label}：{text}" : label;
    }
}

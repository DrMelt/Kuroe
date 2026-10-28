namespace Kuroe.Flow.Shared.StaticGraph.PortModel;

/// <summary>端口表的查找、交回判定与声明文本。</summary>
public static class Ports
{
    /// <summary>执行需经契约工具交回的端口类型：Text 交回内容，Signal 交回端口名或触发值；Context 由来源透传，不需要交回。
    /// Trigger 版与普通版同属一种语义，普通版交回时触发值恒为 true。</summary>
    public static bool RequiresSubmission(PortType type) =>
        type.Semantics is PortSemantics.Text or PortSemantics.Signal;

    /// <summary>端口是否承载文本内容。</summary>
    public static bool CarriesText(PortType type) => type.Semantics == PortSemantics.Text;

    /// <summary>端口是否承载触发值。</summary>
    public static bool CarriesTrigger(PortType type) => type.IsTrigger;

    /// <summary>输出端口表里执行需交回的端口，按声明序。</summary>
    public static IReadOnlyList<Port> RequiringSubmission(IReadOnlyList<Port> ports) =>
        [.. ports.Where(port => RequiresSubmission(port.Type))];

    /// <summary>端口声明的清单，按声明序，有说明写 端口名：说明。</summary>
    public static string List(IEnumerable<Port> ports) => string.Join('、', ports.Select(Describe));

    /// <summary>单个端口的写法：有说明写 端口名：说明，未写说明只写端口名。</summary>
    public static string Describe(Port port) =>
        port.Description is { Length: > 0 } description ? $"{port.Name}：{description}" : port.Name.Value;

    /// <summary>按名字取输出端口声明，未声明时为空。</summary>
    public static Port? Find(this IReadOnlyList<Port> ports, PortName name)
    {
        foreach (Port port in ports)
        {
            if (port.Name == name)
            {
                return port;
            }
        }

        return null;
    }

    /// <summary>按名字取输入端口声明，未声明时为空。</summary>
    public static InputPort? Find(this IReadOnlyList<InputPort> ports, PortName name)
    {
        foreach (InputPort port in ports)
        {
            if (port.Name == name)
            {
                return port;
            }
        }

        return null;
    }

    /// <summary>按输入端口声明显式取文本：静态端口只读静态值，动态端口只读输入值，端口未声明时为空。</summary>
    public static string? ReadText(
        InputPort? declaration,
        PortName port,
        Func<PortName, string?> staticText,
        Func<PortName, string?> dynamicText) =>
        declaration switch
        {
            { IsStatic: true } => staticText(port),
            null => null,
            _ => dynamicText(port),
        };
}

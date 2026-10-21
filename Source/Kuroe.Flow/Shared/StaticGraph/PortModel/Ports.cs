namespace Kuroe.Flow.Shared.StaticGraph.PortModel;

/// <summary>端口表的查找、交回判定与声明文本。</summary>
public static class Ports
{
    /// <summary>模型需经契约工具交回的端口类型：Text 交回内容，Signal 只交回端口名；Context 由来源透传，不需要交回。</summary>
    public static bool RequiresSubmission(PortType type) => type is PortType.Text or PortType.Signal;

    /// <summary>输出端口表里模型需交回的端口，按声明序。</summary>
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
}

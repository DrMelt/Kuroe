namespace Kuroe.Flows.Shared.StaticGraph;

/// <summary>端口表的查找与声明文本。</summary>
public static class Ports
{
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

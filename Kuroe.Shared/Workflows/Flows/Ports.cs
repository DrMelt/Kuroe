namespace Kuroe.Shared.Workflows.Flows;

/// <summary>端口表的查找。</summary>
public static class Ports
{
    /// <summary>按名字取端口声明，未声明时为空。</summary>
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
}

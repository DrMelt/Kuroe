namespace Kuroe.Execution.Shared.Ports;

/// <summary>执行输出端口的查找。</summary>
public static class ExecutionPortRules
{
    /// <summary>按名字取输出端口声明，未声明时为空。</summary>
    public static ExecutionOutputPort? Find(this IReadOnlyList<ExecutionOutputPort> ports, ExecutionPortName name)
    {
        foreach (ExecutionOutputPort port in ports)
        {
            if (port.Name == name)
            {
                return port;
            }
        }

        return null;
    }
}
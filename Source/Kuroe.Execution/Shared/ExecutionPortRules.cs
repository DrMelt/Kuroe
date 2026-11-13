namespace Kuroe.Execution.Shared;

/// <summary>执行输出端口的查找与声明文本，只使用中性端口字段。</summary>
public static class ExecutionPortRules
{
    /// <summary>该执行需交回的输出端口，按声明序。</summary>
    public static IReadOnlyList<ExecutionOutputPort> RequiredFromSubmittable(IReadOnlyList<ExecutionOutputPort> ports) =>
        [.. ports.Where(port => port.Required)];

    /// <summary>端口声明的清单，有说明写 端口名：说明，未写说明只写端口名。</summary>
    public static string List(IEnumerable<ExecutionOutputPort> ports) =>
        string.Join('、', ports.Select(Describe));

    /// <summary>单个端口的写法。</summary>
    public static string Describe(ExecutionOutputPort port) =>
        port.Description is { Length: > 0 } description ? $"{port.Name}：{description}" : port.Name.Value;

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
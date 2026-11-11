using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Graph.Records;

/// <summary>图输入出处与执行记录不透明来源标识之间的转换。标识由本类解释。</summary>
public static class GraphRecordSources
{
    private const char Separator = ':';

    /// <summary>把运行期出处写成执行记录来源标识。</summary>
    public static string Write(ContextSource source) => source switch
    {
        ExecutionSource execution => Join(
            "execution",
            execution.Execution.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            execution.Node.Value,
            execution.Port.Value),
        InputSource input => Join("input", string.Empty, input.Node.Value, input.Port.Value),
        StartSource start => Join("start", string.Empty, string.Empty, start.Port.Value),
        ContainerPortSource container => Join("container", string.Empty, container.Container.Value, container.Port.Value),
        DefaultSource value => Join("default", string.Empty, value.Node.Value, value.Port.Value),
        ConstantSource value => Join("constant", string.Empty, value.Node.Value, value.Port.Value),
        NodeSource node => Join("node", string.Empty, node.Node.Value, node.Port.Value),
        _ => throw new ArgumentException($"未知的输入取值出处 {source.GetType().Name}。"),
    };

    /// <summary>从执行记录来源标识读回运行期出处。</summary>
    public static ContextSource Read(string value)
    {
        string[] parts = value.Split(Separator);
        if (parts.Length != 4)
        {
            throw new FormatException($"执行记录来源 {value} 不合法。");
        }

        string kind = Unescape(parts[0]);
        string execution = Unescape(parts[1]);
        string node = Unescape(parts[2]);
        string port = Unescape(parts[3]);

        return kind switch
        {
            "execution" => new ExecutionSource(
                new ExecutionId(RequiredInt(execution, value)),
                RequiredPath(node, value),
                RequiredPort(port, value)),
            "input" => new InputSource(RequiredPath(node, value), RequiredPort(port, value)),
            "start" => new StartSource(RequiredPort(port, value)),
            "container" => new ContainerPortSource(RequiredPath(node, value), RequiredPort(port, value)),
            "default" => new DefaultSource(RequiredPath(node, value), RequiredPort(port, value)),
            "constant" => new ConstantSource(RequiredPath(node, value), RequiredPort(port, value)),
            "node" => new NodeSource(RequiredPath(node, value), RequiredPort(port, value)),
            _ => throw new FormatException($"执行记录来源 {value} 的种类 {kind} 未知。"),
        };
    }

    private static string Join(string kind, string execution, string node, string port) =>
        string.Join(Separator, Escape(kind), Escape(execution), Escape(node), Escape(port));

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static string Unescape(string value) => Uri.UnescapeDataString(value);

    private static int RequiredInt(string value, string source) =>
        int.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out int result)
            ? result
            : throw new FormatException($"执行记录来源 {source} 缺少执行号。");

    private static NodePath RequiredPath(string value, string source)
    {
        string[] levels = value.Split('/');
        return levels.Length == 0 || levels.All(string.IsNullOrEmpty)
            ? throw new FormatException($"执行记录来源 {source} 缺少节点路径。")
            : new NodePath([.. levels.Select(level => new NodeLabel(level))]);
    }

    private static PortName RequiredPort(string value, string source) =>
        string.IsNullOrEmpty(value)
            ? throw new FormatException($"执行记录来源 {source} 缺少端口。")
            : new PortName(value);
}
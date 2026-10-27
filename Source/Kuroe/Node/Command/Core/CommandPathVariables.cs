using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Shared;

namespace Kuroe.Node.Command;

/// <summary>命令节点的路径变量规则：变量与 Directory 占位符一一对应，映射端口已声明且承载文本。</summary>
internal static class CommandPathVariables
{
    /// <summary>校验声明并返回映射到的输入端口名。</summary>
    internal static IReadOnlySet<string> Validate(
        NodeSpec node,
        CommandParameters? parameters,
        Action<string> report)
    {
        IReadOnlyDictionary<string, string> variables =
            parameters?.Variables ?? new Dictionary<string, string>();
        IReadOnlyList<string> placeholders = parameters?.Directory is { Length: > 0 } directory
            ? PathTemplate.Placeholders(directory)
            : [];

        foreach (string error in PathVariables.Validate(variables, placeholders, portName =>
        {
            InputPort? port = node.Inputs.Find(new PortName(portName));

            return port is null ? null : Ports.CarriesText(port.Value.Type);
        }))
        {
            report(error);
        }

        return new HashSet<string>(variables.Values, StringComparer.Ordinal);
    }
}
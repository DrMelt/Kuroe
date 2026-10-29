using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Kuroe.Shared;

namespace Kuroe.Node.File;

/// <summary>文件节点的路径变量规则：变量与 Path 占位符一一对应，映射端口已声明且承载文本。</summary>
internal static class FilePathVariables
{
    /// <summary>校验声明并返回映射到的输入端口名。</summary>
    internal static IReadOnlySet<string> Validate(
        NodeSpec node,
        FileParameters? parameters,
        Action<string> report)
    {
        IReadOnlyDictionary<string, string> variables =
            parameters?.Variables ?? new Dictionary<string, string>();
        IReadOnlyList<string> placeholders = parameters is null ? [] : PathTemplate.Placeholders(parameters.Path);

        foreach (string error in PathVariables.Validate(variables, placeholders, portName =>
        {
            InputPort? port = node.Inputs.Find(new PortName(portName));

            return port is null ? null : PortRules.CarriesText(port.Value.Type);
        }))
        {
            report(error);
        }

        return new HashSet<string>(variables.Values, StringComparer.Ordinal);
    }
}
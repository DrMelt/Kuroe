using Kuroe.FlowDescription.Definition.Shared.Validation;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.FlowDescription.Definition.Shared.Ports;
using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Runner.Command;
using Kuroe.Shared;

namespace Kuroe.Node.Command;

/// <summary>命令节点的变量规则：变量与 Directory 和 Arguments 的占位符一一对应，映射端口已声明且承载文本。</summary>
internal static class CommandVariables
{
    /// <summary>校验声明并返回映射到的输入端口名。</summary>
    internal static IReadOnlySet<string> Validate(
        NodeSpec node,
        CommandExecutionParameters? parameters,
        ValidationMessageHandler fail)
    {
        IReadOnlyDictionary<string, string> variables =
            parameters?.Variables ?? new Dictionary<string, string>();
        IReadOnlyList<string> placeholders = parameters is null
            ? []
            : [.. PathTemplate.Placeholders(parameters.Directory ?? string.Empty)
                .Concat(parameters.Arguments.SelectMany(PathTemplate.Placeholders))
                .Distinct()];

        foreach (string error in VariableMappings.Validate(variables, placeholders, portName =>
        {
            InputPort? port = node.Inputs.Find(new PortName(portName));

            return port is null ? null : PortRules.CarriesText(port.Value.Type);
        }, "命令", "Directory 或 Arguments"))
        {
            fail(error);
        }

        return new HashSet<string>(variables.Values, StringComparer.Ordinal);
    }
}
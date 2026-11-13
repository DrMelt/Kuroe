using ErrorOr;
using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.StaticGraph.Nodes;
using Kuroe.Flow.Node.StaticGraph.Ports;
using Kuroe.Shared;

namespace Kuroe.Node.Command;

/// <summary>命令节点的变量规则：变量与 Directory 和 Arguments 的占位符一一对应，映射端口已声明且承载文本。</summary>
internal static class CommandVariables
{
    /// <summary>命令路径解析错误的错误码。</summary>
    private const string PathErrorCode = "Command.Path";

    /// <summary>命令参数展开错误的错误码。</summary>
    private const string ArgumentErrorCode = "Command.Argument";

    /// <summary>校验声明并返回映射到的输入端口名。</summary>
    internal static IReadOnlySet<string> Validate(
        NodeSpec node,
        CommandParameters? parameters,
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

    /// <summary>从映射端口读出变量原文，root 变量解析为工作区内的相对路径基准。</summary>
    internal static ErrorOr<CommandVariableValues> Resolve(
        IReadOnlyDictionary<string, string> variables,
        Func<string, string?> portText,
        string rootDirectory)
    {
        Dictionary<string, string> values = [];
        foreach ((string name, string port) in variables)
        {
            string? value = portText(port);
            if (string.IsNullOrWhiteSpace(value))
            {
                return Error.Validation(PathErrorCode, $"命令变量 {name} 未连接或为空。");
            }

            values[name] = value;
        }

        string baseDirectory = rootDirectory;
        if (values.TryGetValue(PathVariables.RootName, out string? root)
            && !WorkspacePath.TryResolve(rootDirectory, root, out baseDirectory))
        {
            return Error.Validation(PathErrorCode, $"命令变量 root 的值 {root} 不是工作区内的合法路径。");
        }

        return new CommandVariableValues(values, baseDirectory);
    }

    /// <summary>展开命令的执行目录：root 替换为工作区内的绝对基准，其他变量保持原文，结果必须落在工作区根内。</summary>
    internal static ErrorOr<string> ExpandDirectory(
        string template,
        CommandVariableValues values,
        string rootDirectory)
    {
        Dictionary<string, string> variables = [];
        foreach ((string name, string value) in values.Variables)
        {
            variables[name] = value;
        }

        if (variables.ContainsKey(PathVariables.RootName))
        {
            variables[PathVariables.RootName] = values.BaseDirectory;
        }

        return PathVariables.Expand(template, variables, values.BaseDirectory, rootDirectory, "Directory");
    }

    /// <summary>展开命令参数，含未映射占位符的参数给出错误。</summary>
    internal static ErrorOr<IReadOnlyList<string>> ExpandArguments(
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string> variables)
    {
        List<string> expanded = [];
        foreach (string argument in arguments)
        {
            string? missing = PathTemplate.Placeholders(argument)
                .FirstOrDefault(placeholder => !variables.ContainsKey(placeholder));
            if (missing is not null)
            {
                return Error.Validation(ArgumentErrorCode, $"命令参数 {argument} 含未映射的变量 {missing}。");
            }

            expanded.Add(PathTemplate.Expand(argument, variables));
        }

        return expanded;
    }
}

/// <summary>命令节点的变量取值：变量名到原文的映射与相对路径的解析基准。</summary>
/// <param name="Variables">变量名到原文的映射。</param>
/// <param name="BaseDirectory">相对路径的解析基准，没有 root 变量时是工作区根。</param>
internal sealed record CommandVariableValues(
    IReadOnlyDictionary<string, string> Variables,
    string BaseDirectory);
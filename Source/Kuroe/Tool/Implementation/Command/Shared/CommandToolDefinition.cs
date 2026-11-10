using System.Text;
using System.Text.RegularExpressions;
using ErrorOr;

namespace Kuroe.Tool.Command.Shared;

/// <summary>命令工具的配置定义：路径、说明、参数、命令模板与执行参数。模板的每一项是一个参数项，
/// 里面的 {参数名} 是占位符，列表参数按元素重复整个参数项；函数定义按这些数据交给工具契约。</summary>
/// <param name="Path">函数在工具层级里的完整路径。</param>
/// <param name="Description">给模型的说明。</param>
/// <param name="Parameters">参数声明。</param>
/// <param name="Template">命令行模板，数组每一项是一个参数项。</param>
/// <param name="Directory">命令执行目录，相对执行基准；未给时执行目录即基准。</param>
/// <param name="TimeoutSeconds">命令超时秒数。</param>
/// <param name="OutputLimit">命令输出上限。</param>
/// <param name="Variables">路径变量名，值由使用处的端口映射提供。</param>
/// <param name="Groups">互斥参数组，缺省时组内参数可以任意组合。</param>
public sealed record CommandToolDefinition(
    ToolPath Path,
    string Description,
    IReadOnlyList<ToolParameter> Parameters,
    IReadOnlyList<CommandToolTemplateItem> Template,
    string? Directory = null,
    int TimeoutSeconds = CommandToolTemplate.DefaultTimeoutSeconds,
    int OutputLimit = CommandToolTemplate.DefaultOutputLimit,
    IReadOnlyList<string>? Variables = null,
    IReadOnlyList<ToolParameterGroup>? Groups = null)
{
    /// <summary>路径变量名集合，未声明时为空。</summary>
    public IReadOnlyList<string> PathVariables => Variables ?? [];

    /// <summary>交给工具契约的函数定义：路径、说明、参数、变量与互斥参数组。</summary>
    public ToolDefinition Definition => new(Path, Description, Parameters, PathVariables, Groups);

    /// <summary>按模板与实参展开命令行，第一项是程序本身。返回非空表示实参缺项或模板不合法。</summary>
    public ErrorOr<IReadOnlyList<string>> CommandLine(ToolArguments arguments) =>
        CommandLine(arguments, new Dictionary<string, string>());

    /// <summary>按模板、实参与路径变量展开命令行，变量优先于同名参数声明。第一项是程序本身。</summary>
    public ErrorOr<IReadOnlyList<string>> CommandLine(
        ToolArguments arguments,
        IReadOnlyDictionary<string, string> variables)
    {
        var argv = new List<string>();

        foreach (CommandToolTemplateItem item in Template)
        {
            Error? rejected = ExpandItem(Parameters, arguments, variables, item, argv);
            if (rejected is not null)
            {
                return rejected.Value;
            }
        }

        if (argv.Count == 0)
        {
            return ToolErrors.Argument("模板展开后没有任何可执行的程序。");
        }

        return argv;
    }

    /// <summary>展开一个参数项。对象项的关联参数缺省时整项消失，必填仍拒绝；其余参数项按占位符展开。返回非空表示中断调用。</summary>
    private static Error? ExpandItem(
        IReadOnlyList<ToolParameter> parameters,
        ToolArguments arguments,
        IReadOnlyDictionary<string, string> variables,
        CommandToolTemplateItem item,
        List<string> argv)
    {
        if (item.OmitWhenMissing is { } linkedName)
        {
            ToolParameter? linked = parameters.FirstOrDefault(candidate => candidate.Name.Value == linkedName);
            if (linked is not null && !linked.IsGiven(arguments))
            {
                return linked.Required ? ToolErrors.Argument($"缺少参数 {linkedName}。") : null;
            }
        }

        if (CommandToolTemplate.PlaceholdersOf(item.Text).Count == 0)
        {
            argv.Add(item.Text);

            return null;
        }

        return ExpandToken(parameters, arguments, variables, item, argv);
    }

    /// <summary>展开一个含占位符的参数项。变量优先，其次才是参数声明。列表参数按索引配对后重复整个参数项。
    /// 展开后追加参数项，返回非空表示中断调用。</summary>
    private static Error? ExpandToken(
        IReadOnlyList<ToolParameter> parameters,
        ToolArguments arguments,
        IReadOnlyDictionary<string, string> variables,
        CommandToolTemplateItem item,
        List<string> argv)
    {
        string token = item.Text;
        MatchCollection matches = CommandToolTemplate.Matches(token);

        // 独立占位符：整个参数项就是 {name}。
        if (matches.Count == 1 && matches[0].Index == 0 && matches[0].Length == token.Length)
        {
            string name = matches[0].Groups[1].Value;
            if (variables.TryGetValue(name, out string? variable))
            {
                argv.Add(variable);

                return null;
            }

            ToolParameter? parameter = parameters.FirstOrDefault(candidate => candidate.Name.Value == name);
            if (parameter is null)
            {
                return ToolErrors.Internal($"模板占位符 {{{name}}} 没有对应参数声明。");
            }

            if (parameter.List)
            {
                List<string> values = [.. arguments.List(parameter.Name) ?? []];
                if (values.Count == 0)
                {
                    return parameter.Required ? ToolErrors.Argument($"缺少参数 {name}。") : null;
                }

                foreach (string value in values)
                {
                    Error? rejectedValue = CheckLeadingDash(parameter, value);
                    if (rejectedValue is not null)
                    {
                        return rejectedValue;
                    }
                }

                argv.AddRange(values);

                return null;
            }

            string? text = arguments.Text(parameter.Name);
            if (text is null)
            {
                return parameter.Required ? ToolErrors.Argument($"缺少参数 {name}。") : null;
            }

            Error? rejectedText = CheckLeadingDash(parameter, text);
            if (rejectedText is not null)
            {
                return rejectedText;
            }

            argv.Add(text);

            return null;
        }

        // 内嵌占位符：变量与标量按一次求值，列表参数按索引配对后重复整个参数项。
        Dictionary<string, string> constantValues = new(variables);
        List<(string Name, ToolParameter Parameter)> listParameters = [];

        foreach (Match match in matches)
        {
            string name = match.Groups[1].Value;
            if (variables.ContainsKey(name))
            {
                continue;
            }

            ToolParameter? parameter = parameters.FirstOrDefault(candidate => candidate.Name.Value == name);
            if (parameter is null)
            {
                return ToolErrors.Internal($"模板占位符 {{{name}}} 没有对应参数声明。");
            }

            if (parameter.List)
            {
                if (listParameters.All(candidate => candidate.Name != name))
                {
                    listParameters.Add((name, parameter));
                }

                continue;
            }

            string? value = arguments.Text(parameter.Name);
            if (value is null)
            {
                if (parameter.Required)
                {
                    return ToolErrors.Argument($"缺少参数 {name}。");
                }

                continue;
            }

            Error? rejectedValue = CheckLeadingDash(parameter, value);
            if (rejectedValue is not null)
            {
                return rejectedValue;
            }

            constantValues[name] = value;
        }

        if (listParameters.Count == 0)
        {
            string expanded = ExpandText(token, matches, constantValues);
            if (expanded.Length > 0)
            {
                argv.Add(expanded);
            }

            return null;
        }

        foreach (Match match in matches)
        {
            string name = match.Groups[1].Value;
            if (variables.ContainsKey(name) || listParameters.Any(candidate => candidate.Name == name))
            {
                continue;
            }

            if (!constantValues.ContainsKey(name)
                && parameters.Any(candidate => candidate.Name.Value == name))
            {
                return ToolErrors.Argument(
                    $"参数 {name} 缺失，不能与列表参数 {listParameters[0].Name} 在同一参数项展开。");
            }
        }

        List<List<string>> listValues = [];
        foreach ((string name, ToolParameter parameter) in listParameters)
        {
            List<string> values = [.. arguments.List(parameter.Name) ?? []];
            if (values.Count == 0 && parameter.Required)
            {
                return ToolErrors.Argument($"缺少参数 {name}。");
            }

            foreach (string value in values)
            {
                Error? rejectedValue = CheckLeadingDash(parameter, value);
                if (rejectedValue is not null)
                {
                    return rejectedValue;
                }
            }

            listValues.Add(values);
        }

        int count = listValues[0].Count;
        for (int index = 1; index < listValues.Count; index++)
        {
            if (listValues[index].Count != count)
            {
                return ToolErrors.Argument(
                    $"列表参数 {listParameters[0].Name} 与 {listParameters[index].Name} 的元素数量必须一致。");
            }
        }

        for (int index = 0; index < count; index++)
        {
            for (int position = 0; position < listParameters.Count; position++)
            {
                constantValues[listParameters[position].Name] = listValues[position][index];
            }

            string expanded = ExpandText(token, matches, constantValues);
            if (expanded.Length > 0)
            {
                argv.Add(expanded);
            }
        }

        return null;
    }

    /// <summary>按原匹配位置替换一次，替换值里的占位符不再展开。</summary>
    private static string ExpandText(string token, MatchCollection matches, Dictionary<string, string> values)
    {
        var expanded = new StringBuilder();
        int position = 0;

        foreach (Match match in matches)
        {
            expanded.Append(token, position, match.Index - position);

            string name = match.Groups[1].Value;
            if (values.TryGetValue(name, out string? value))
            {
                expanded.Append(value);
            }
            else
            {
                expanded.Append(match.Value);
            }

            position = match.Index + match.Length;
        }

        expanded.Append(token, position, token.Length - position);

        return expanded.ToString();
    }

    /// <summary>参数值不能以 - 开头，否则会被程序当成开关；声明允许的参数除外。</summary>
    private static Error? CheckLeadingDash(ToolParameter parameter, string value) =>
        !parameter.AllowLeadingDash && value.StartsWith('-')
            ? ToolErrors.Argument($"参数 {parameter.Name.Value} 的值不能以 - 开头。")
            : null;
}

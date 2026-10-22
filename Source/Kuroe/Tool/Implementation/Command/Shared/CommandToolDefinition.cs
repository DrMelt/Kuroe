using System.Text.RegularExpressions;
using ErrorOr;

namespace Kuroe.Tool.Command.Shared;

/// <summary>命令工具的配置定义：路径、说明、参数、命令模板与执行参数。模板的每一项是一个参数项，
/// 里面的 {参数名} 是占位符，运行时由模型实参填充；函数定义按这些数据交给工具契约。</summary>
/// <param name="Path">函数在工具层级里的完整路径。</param>
/// <param name="Description">给模型的说明。</param>
/// <param name="Parameters">参数声明。</param>
/// <param name="Template">命令行模板，数组每一项是一个参数项。</param>
/// <param name="Directory">命令执行目录，相对工作目录；未给时用工作目录根。</param>
/// <param name="TimeoutSeconds">命令超时秒数。</param>
/// <param name="OutputLimit">交回文本上限。</param>
public sealed record CommandToolDefinition(
    ToolPath Path,
    string Description,
    IReadOnlyList<CommandToolParameter> Parameters,
    IReadOnlyList<CommandToolTemplateItem> Template,
    string? Directory = null,
    int TimeoutSeconds = CommandToolTemplate.DefaultTimeoutSeconds,
    int OutputLimit = CommandToolTemplate.DefaultOutputLimit)
{
    /// <summary>交给工具契约的函数定义：路径、说明与参数。</summary>
    public ToolDefinition Definition =>
        new(Path, Description, [.. Parameters.Select(ToContract)]);

    /// <summary>按模板与实参展开命令行，第一项是程序本身。返回非空表示实参缺项或模板不合法。</summary>
    public ErrorOr<IReadOnlyList<string>> CommandLine(ToolArguments arguments)
    {
        IReadOnlyList<ToolParameter> parameters = Definition.Parameters;
        var argv = new List<string>();

        foreach (CommandToolTemplateItem item in Template)
        {
            Error? rejected = ExpandItem(parameters, arguments, item, argv);
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
    private static Error? ExpandItem(IReadOnlyList<ToolParameter> parameters, ToolArguments arguments, CommandToolTemplateItem item, List<string> argv)
    {
        if (item.OmitWhenMissing is { } linkedName)
        {
            ToolParameter? linked = parameters.FirstOrDefault(candidate => candidate.Name.Value == linkedName);
            if (linked is not null && IsMissing(arguments, linked))
            {
                return linked.Required ? ToolErrors.Argument($"缺少参数 {linkedName}。") : null;
            }
        }

        if (CommandToolTemplate.PlaceholdersOf(item.Text).Count == 0)
        {
            argv.Add(item.Text);

            return null;
        }

        return ExpandToken(parameters, arguments, item.Text, argv);
    }

    /// <summary>关联参数是否缺省：布尔参数为真才算给出，列表参数为空算缺省，文本参数未给算缺省。</summary>
    private static bool IsMissing(ToolArguments arguments, ToolParameter parameter) => parameter switch
    {
        { Flag: true } => arguments.Flag(parameter.Name) != true,
        { List: true } => (arguments.List(parameter.Name) ?? []).Count == 0,
        _ => arguments.Text(parameter.Name) is null,
    };

    /// <summary>展开一个含占位符的参数项。展开后追加参数项，返回非空表示中断调用。</summary>
    private static Error? ExpandToken(IReadOnlyList<ToolParameter> parameters, ToolArguments arguments, string token, List<string> argv)
    {
        // 独立占位符：整个参数项就是 {name}。列表参数只允许这种写法。
        MatchCollection matches = CommandToolTemplate.Matches(token);
        if (matches.Count == 1 && token.StartsWith('{') && token.EndsWith('}'))
        {
            string name = matches[0].Groups[1].Value;
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

                argv.AddRange(values);

                return null;
            }

            string? text = arguments.Text(parameter.Name);
            if (text is null)
            {
                return parameter.Required ? ToolErrors.Argument($"缺少参数 {name}。") : null;
            }

            argv.Add(text);

            return null;
        }

        // 内嵌占位符：只允许标量参数，逐个替换；可选参数缺省时该占位符按字面量保留。
        string expanded = token;
        foreach (Match match in matches)
        {
            string name = match.Groups[1].Value;
            ToolParameter? parameter = parameters.FirstOrDefault(candidate => candidate.Name.Value == name);
            if (parameter is null)
            {
                return ToolErrors.Internal($"模板占位符 {{{name}}} 没有对应参数声明。");
            }

            if (parameter.List)
            {
                return ToolErrors.Internal($"列表参数 {name} 只能写成独立的 {{{name}}} 参数项。");
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

            expanded = expanded.Replace(match.Value, value, StringComparison.Ordinal);
        }

        if (expanded.Length > 0)
        {
            argv.Add(expanded);
        }

        return null;
    }

    /// <summary>命令侧的参数声明转成工具契约的声明。</summary>
    private static ToolParameter ToContract(CommandToolParameter parameter) =>
        new(new ToolName(parameter.Name), parameter.Description,
            Flag: parameter.Flag, Required: parameter.Required, List: parameter.List);
}

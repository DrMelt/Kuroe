using Kuroe.Shared;

namespace Kuroe.Tool.Command.Shared;

/// <summary>命令定义的模板一致性：模板、参数声明与路径变量必须互相吻合。</summary>
public static class CommandToolValidation
{
    /// <summary>模板与参数声明的不一致原因，按声明顺序给出。</summary>
    public static IReadOnlyList<string> TemplateReasons(CommandToolDefinition definition)
    {
        List<string> reasons = [];
        IReadOnlyList<ToolParameter> parameters = definition.Parameters;
        IReadOnlyList<string> variables = definition.PathVariables;
        IReadOnlyList<CommandToolTemplateItem> template = definition.Template;
        IReadOnlyList<string> placeholders = [.. CommandToolTemplate.PlaceholdersOf(template)];

        reasons.AddRange(variables
            .Where(variable => parameters.Any(parameter => parameter.Name.Value == variable))
            .Select(variable => $"路径变量 {variable} 与参数同名。"));

        foreach (CommandToolTemplateItem item in template)
        {
            if (item.OmitWhenMissing is not { } linkedName)
            {
                continue;
            }

            ToolParameter? linked = parameters.FirstOrDefault(parameter => parameter.Name.Value == linkedName);
            if (linked is null)
            {
                reasons.Add($"模板参数项关联的参数 {linkedName} 没有对应声明。");
            }
            else if (linked.Flag && item.Text.Contains($"{{{linkedName}}}", StringComparison.Ordinal))
            {
                reasons.Add($"布尔参数 {linkedName} 不能出现在参数项的文字里。");
            }
        }

        reasons.AddRange(placeholders
            .Where(placeholder => !variables.Contains(placeholder)
                && parameters.All(parameter => parameter.Name.Value != placeholder))
            .Select(placeholder => $"模板占位符 {{{placeholder}}} 没有对应的参数或路径变量声明。"));

        // root 是执行基准的保留变量，未写进模板也由命令执行读取。
        IReadOnlyList<string> directoryVariables = [.. PathTemplate.Placeholders(definition.Directory ?? string.Empty)];
        reasons.AddRange(variables
            .Where(variable => variable != PathVariables.RootName
                && !placeholders.Contains(variable)
                && !directoryVariables.Contains(variable))
            .Select(variable => $"路径变量 {variable} 没有被模板或 Directory 引用。"));

        reasons.AddRange(parameters
            .Where(parameter => !parameter.Flag && !placeholders.Contains(parameter.Name.Value))
            .Select(parameter => $"参数 {parameter.Name.Value} 已声明但没有出现在模板里。"));

        reasons.AddRange(parameters
            .Where(parameter => parameter.Flag && placeholders.Contains(parameter.Name.Value))
            .Select(parameter => $"布尔参数 {parameter.Name.Value} 只能关联参数项，不能写成占位符。"));

        reasons.AddRange(parameters
            .Where(parameter => parameter.Flag
                && !template.Any(item => item.OmitWhenMissing == parameter.Name.Value))
            .Select(parameter => $"布尔参数 {parameter.Name.Value} 没有被任何模板项关联。"));

        return reasons;
    }

    /// <summary>校验一组定义，任一不一致时抛出 <see cref="InvalidOperationException"/>。</summary>
    public static void EnsureValid(IEnumerable<CommandToolDefinition> definitions)
    {
        List<string> reasons =
        [
            .. definitions.SelectMany(definition =>
                TemplateReasons(definition)
                    .Select(reason => $"命令工具 {definition.Path.Value}：{reason}")),
        ];

        if (reasons.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, reasons));
        }
    }
}
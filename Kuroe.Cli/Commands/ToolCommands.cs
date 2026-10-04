using Kuroe.Shared.Executions.Tools;
using Kuroe.Tools.CommandTools;
using Spectre.Console;

namespace Kuroe.Cli.Commands;

/// <summary>/tool 子命令的解析与执行，列出配置的命令工具。</summary>
internal sealed class ToolCommands(
    IReadOnlyList<CommandToolDefinition> commandTools,
    Terminal terminal)
{
    /// <summary>该命令族的帮助行。</summary>
    public static IReadOnlyList<(string Command, string Description)> Help { get; } =
    [
        ("/tool list", "列出命令工具"),
    ];

    public void Run(string[] parts)
    {
        const string usage = "用法：/tool list";
        string subcommand = parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty;

        switch ((subcommand, parts.Length))
        {
            case ("list", 2):
                List();
                break;

            default:
                terminal.Hint(usage);
                break;
        }
    }

    private void List()
    {
        if (commandTools.Count == 0)
        {
            terminal.Hint("没有配置命令工具。写在工作目录 .kuroe/tools.json，见工具扩展文档。");
            return;
        }

        terminal.Line("命令工具：");
        Grid grid = Terminal.Columns(4, wrapColumns: 3);
        grid.AddRow(
            new Text("名称", Styles.Hint),
            new Text("说明", Styles.Hint),
            new Text("模板", Styles.Hint),
            new Text("参数", Styles.Hint));

        foreach (CommandToolDefinition tool in commandTools)
        {
            grid.AddRow(
                new Text(tool.Name.Value, Styles.Key),
                new Text(tool.Description),
                new Text(Template(tool.Template), Styles.Hint),
                new Text(Parameters(tool.Parameters), Styles.Hint));
        }

        terminal.NewLine();
        terminal.Write(grid);
    }

    /// <summary>模板呈现：对象项在文字后加 ? 表示关联参数缺省时整项消失。</summary>
    private static string Template(IReadOnlyList<CommandToolTemplateItem> template) =>
        string.Join(" ", template.Select(item => item.OmitWhenMissing is null ? item.Text : $"{item.Text}?"));

    /// <summary>参数呈现：名字与是否列表、是否必填。</summary>
    private static string Parameters(IReadOnlyList<ToolParameter> parameters)
    {
        if (parameters.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("、", parameters.Select(parameter =>
            parameter.Name.Value
            + (parameter.List ? "[]" : string.Empty)
            + (parameter.Required ? "*" : string.Empty)));
    }
}

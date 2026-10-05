using Kuroe.Tools.CommandTools;
using Spectre.Console;

namespace Kuroe.Cli.Commands;

/// <summary>/tool 子命令的解析与执行，列出配置的命令工具及路径。</summary>
internal sealed class ToolCommands(
    IReadOnlyList<CommandToolDefinition> commandTools,
    Terminal terminal)
{
    /// <summary>该命令族的帮助行。</summary>
    public static IReadOnlyList<(string Command, string Description)> Help { get; } =
    [
        ("/tool list", "列出配置的命令工具及路径"),
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
        Grid grid = Terminal.Columns(5, wrapColumns: 4);
        grid.AddRow(
            new Text("路径", Styles.Hint),
            new Text("名称", Styles.Hint),
            new Text("说明", Styles.Hint),
            new Text("模板", Styles.Hint),
            new Text("参数", Styles.Hint));

        foreach (CommandToolDefinition tool in commandTools)
        {
            grid.AddRow(
                new Text(tool.FullPath.Value, Styles.Hint),
                new Text(tool.Name.Value, Styles.Key),
                new Text(tool.Description),
                new Text(CommandToolPresentation.Template(tool.Template), Styles.Hint),
                new Text(CommandToolPresentation.Parameters(tool.Parameters), Styles.Hint));
        }

        terminal.NewLine();
        terminal.Write(grid);
    }
}

using Kuroe.Presentation;
using Kuroe.Tool.Json;
using Spectre.Console;

namespace Kuroe.Cli.Commands;

/// <summary>/tool 子命令的解析与执行，列出配置的命令工具及路径。</summary>
internal sealed class ToolCommands(
    IReadOnlyList<ConfiguredTool> configuredTools,
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
        if (configuredTools.Count == 0)
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

        foreach (ConfiguredTool tool in configuredTools)
        {
            grid.AddRow(
                new Text(tool.Definition.Path.Value, Styles.Hint),
                new Text(tool.Definition.Path.Name.Value, Styles.Key),
                new Text(tool.Definition.Description),
                new Text(tool.Configuration, Styles.Hint),
                new Text(tool.Definition.Parameters.Labels(), Styles.Hint));
        }

        terminal.NewLine();
        terminal.Write(grid);
    }
}

using Kuroe.Console.Views;
using Kuroe.Presentation;
using Kuroe.Tool.Json;
using Spectre.Console;

namespace Kuroe.Console.Commands;

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

        Table table = Blocks.Table(
            ("路径", true),
            ("名称", false),
            ("说明", true),
            ("模板", true),
            ("参数", true));

        foreach (ConfiguredTool tool in configuredTools)
        {
            table.AddRow(
                new Text(tool.Definition.Path.Value, Styles.Hint),
                new Text(tool.Definition.Path.Name.Value, Styles.Key),
                new Text(tool.Definition.Description),
                new Text(tool.Configuration, Styles.Hint),
                new Text(tool.Definition.Parameters.Labels(), Styles.Hint));
        }

        terminal.Write(Blocks.Section(BoxKind.View, $"命令工具 {configuredTools.Count} 个", table));
    }
}

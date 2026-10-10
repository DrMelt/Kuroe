using Kuroe.Catalogs;
using Kuroe.Cli.Views;
using Kuroe.Workflows.TaskExecution;
using Spectre.Console;

namespace Kuroe.Cli.Commands;

/// <summary>启动提示与斜杠命令的分发，各命令族的解析与执行在对应的类型里。</summary>
internal sealed class ReplCommands(
    DialogueHost dialogue,
    ModelService models,
    ProviderCommands providers,
    ModelCommands modelCommands,
    CatalogCommands catalogs,
    SettingsCommands settingsCommands,
    TaskCommands tasks,
    FlowCommands flowCommands,
    ToolCommands toolCommands,
    Terminal terminal,
    ErrorView errors)
{
    /// <summary>不属于任何命令族的帮助行。</summary>
    private static readonly (string Command, string Description)[] OwnHelp =
    [
        ("/reset", "重置对话，回到起点"),
        ("/exit", "退出"),
    ];

    /// <summary>分发斜杠命令；命令要求退出时返回 true。</summary>
    public bool Execute(string input)
    {
        string[] parts = Arguments.Split(input);

        switch (parts[0].ToLowerInvariant())
        {
            case "/exit":
                return true;

            case "/help":
                PrintHelp();
                break;

            case "/reset":
                Reset();
                break;

            case "/config":
                settingsCommands.PrintConfig();
                break;

            case "/provider":
                providers.Run(parts);
                break;

            case "/model":
                modelCommands.Run(parts);
                break;

            case "/catalog":
                catalogs.Run(parts);
                break;

            case "/task":
                tasks.Run(parts);
                break;

            case "/flow":
                flowCommands.Run(parts);
                break;

            case "/tool":
                toolCommands.Run(parts);
                break;

            case "/set":
                settingsCommands.Set(parts);
                break;

            case "/unset":
                settingsCommands.Unset(parts);
                break;

            default:
                terminal.Warn($"未知命令 {parts[0]}，输入 /help 查看命令。");
                break;
        }

        return false;
    }

    /// <summary>重置对话任务，回到起点。</summary>
    private void Reset()
    {
        dialogue.Reset();
        terminal.Ok("对话已重置。");
    }

    /// <summary>当前模型无法连接时的注册指引，模型可用时没有输出。</summary>
    public void GuideCurrentModel() => errors.GuideModelRegistration(models, models.Current?.Value);

    /// <summary>各命令族的帮助行按固定顺序汇总，命令列与说明列由栅格对齐。</summary>
    private void PrintHelp()
    {
        (string Command, string Description)[] rows =
        [
            .. TaskCommands.Help,
            .. FlowCommands.Help,
            .. ToolCommands.Help,
            .. SettingsCommands.Help,
            .. ProviderCommands.Help,
            .. ModelCommands.Help,
            .. CatalogCommands.Help,
            .. OwnHelp,
        ];

        Grid grid = Terminal.Columns(2, wrapColumns: 1);
        foreach ((string command, string description) in rows)
        {
            grid.AddRow(new Text(command, Styles.Key), new Text(description));
        }

        terminal.NewLine();
        terminal.Write(grid);
    }
}

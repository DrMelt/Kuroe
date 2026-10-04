using Kuroe.Catalogs;
using Kuroe.Cli.Views;
using Kuroe.Workflows.Tasks;
using Spectre.Console;

namespace Kuroe.Cli.Commands;

/// <summary>启动提示与斜杠命令的分发，各命令族的解析与执行在对应的类型里。</summary>
internal sealed class ReplCommands(
    TaskRegistry registry,
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
        ("/reset", "清空当前任务的上下文"),
        ("exit", "退出"),
    ];

    public void Execute(string input)
    {
        string[] parts = Arguments.Split(input);

        switch (parts[0].ToLowerInvariant())
        {
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
    }

    /// <summary>清空当前任务的上下文。没有任务时给出提示。</summary>
    private void Reset()
    {
        if (registry.Active is not { } id || registry.Find(id) is not { IsError: false } found)
        {
            terminal.Hint("还没有任务，先 /task new <目标> 提交一个。");
            return;
        }

        found.Value.ResetDialogue();
        terminal.Ok($"{found.Value.Id} 的上下文已清空。");
    }

    /// <summary>当前模型无法连接时的注册指引，模型可用时没有输出。</summary>
    public void GuideCurrentModel() => errors.GuideModelRegistration(models, models.Current);

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

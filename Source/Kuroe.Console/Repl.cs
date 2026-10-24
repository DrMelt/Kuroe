using Kuroe.Console.Views;
using Kuroe.Flow.Runtime.Tasks;

namespace Kuroe.Console;

/// <summary>终端交互循环。主菜单是唯一入口，对话作为菜单项，普通输入进入常驻对话任务。</summary>
internal sealed class Repl(
    TaskService tasks,
    DialogueBrowser dialogueBrowser,
    ExecutionNotifier notifier,
    StartupView startup,
    TaskBrowser taskBrowser,
    FlowBrowser flowBrowser,
    CatalogBrowser catalogBrowser,
    SettingsBrowser settingsBrowser,
    ToolView toolView,
    Terminal terminal)
{
    private sealed record Item(string Label, string Action);

    private static readonly Item Exit = new("退出程序", "exit");

    private static readonly Item[] Items =
    [
        new("Kuroe 助手", "dialogue"),
        new("任务", "task"),
        new("流程", "flow"),
        new("工具", "tool"),
        new("模型与提供商", "catalog"),
        new("设置", "settings"),
        Exit,
    ];

    public async Task RunAsync()
    {
        notifier.Attach();
        startup.Print();

        while (true)
        {
            terminal.Page("主菜单");

            Item picked;
            using (terminal.Exclusive())
            {
                picked = terminal.Choose("选择操作", Items, item => item.Label, Exit);
            }

            switch (picked.Action)
            {
                case "exit":
                    if (!ConfirmExit())
                    {
                        break;
                    }

                    await tasks.ShutdownAsync();
                    return;

                case "dialogue":
                    await dialogueBrowser.BrowseAsync();
                    break;

                case "task":
                    taskBrowser.Browse();
                    break;

                case "flow":
                    flowBrowser.Browse();
                    break;

                case "tool":
                    using (terminal.Exclusive())
                    {
                        ViewPage.Browse(terminal, "工具", toolView.Print);
                    }

                    break;

                case "catalog":
                    catalogBrowser.Browse();
                    break;

                case "settings":
                    settingsBrowser.Browse();
                    break;
            }
        }
    }

    /// <summary>退出会取消全部任务，选择后要求再次确认，默认停在返回主菜单的选项。</summary>
    private bool ConfirmExit()
    {
        using (terminal.Exclusive())
        {
            return Confirmation.Ask(terminal, "退出并取消全部任务？", "× 确认退出", "← 返回主菜单");
        }
    }
}


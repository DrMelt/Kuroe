using ApiHub.Models;
using Kuroe.Agent;
using Kuroe.Catalogs;
using Kuroe.Configuration;

namespace Kuroe.Cli.Commands;

/// <summary>启动提示与斜杠命令的分发，各命令族的解析与执行在对应的类型里。</summary>
internal sealed class ReplCommands(
    AgentSession session,
    SettingsProvider settings,
    CatalogService catalog,
    ToolCollection tools,
    ProviderCommands providers,
    ModelCommands models,
    CatalogCommands catalogs,
    SettingsCommands settingsCommands)
{
    /// <summary>不属于任何命令族的帮助行。</summary>
    private static readonly (string Command, string Description)[] OwnHelp =
    [
        ("/reset", "清空上下文"),
        ("exit", "退出"),
    ];

    private readonly AgentSession _session = session;
    private readonly SettingsProvider _settings = settings;
    private readonly CatalogService _catalog = catalog;
    private readonly ToolCollection _tools = tools;
    private readonly ProviderCommands _providers = providers;
    private readonly ModelCommands _models = models;
    private readonly CatalogCommands _catalogs = catalogs;
    private readonly SettingsCommands _settingsCommands = settingsCommands;

    public void Execute(string input)
    {
        string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        switch (parts[0].ToLowerInvariant())
        {
            case "/help":
                PrintHelp();
                break;

            case "/reset":
                _session.Reset();
                Console.WriteLine("上下文已清空。");
                break;

            case "/config":
                _settingsCommands.PrintConfig();
                break;

            case "/provider":
                _providers.Run(parts);
                break;

            case "/model":
                _models.Run(parts);
                break;

            case "/catalog":
                _catalogs.Run(parts);
                break;

            case "/set":
                _settingsCommands.Set(input);
                break;

            case "/unset":
                _settingsCommands.Unset(input);
                break;

            default:
                Console.WriteLine($"未知命令 {parts[0]}，输入 /help 查看命令。");
                break;
        }
    }

    /// <summary>启动横幅：当前模型、目录规模，以及缺少提供商或工具时的处理指引。</summary>
    public void PrintStartup()
    {
        CatalogContents contents = _catalog.Snapshot();
        string model = _settings.Current.Agent.Model?.Value ?? "未选择";
        Console.WriteLine($"Kuroe 已启动，当前模型 {model}，" +
            $"目录中有 {contents.Providers.Length} 个提供商、{contents.Models.Length} 个模型。");
        if (contents.Providers.Length == 0)
        {
            Console.WriteLine("先 /provider add <提供商> <端点> <凭据> 添加提供商，再 /model add <模型> <提供商> 注册模型。");
        }

        if (_tools.Tools.Count == 0)
        {
            Console.WriteLine("当前没有可用工具，检查是否注册了工具载体、公开方法是否标注 DescriptionAttribute。");
        }

        Console.WriteLine("输入 exit 退出，/help 查看命令，Ctrl+C 中断当前回复。");
    }

    /// <summary>当前模型无法连接时的注册指引，模型可用时没有输出。</summary>
    public void GuideCurrentModel() => ConsoleErrors.GuideModelRegistration(_catalog, _settings.Current.Agent.Model);

    /// <summary>各命令族的帮助行按固定顺序汇总，命令列宽统一。</summary>
    private static void PrintHelp()
    {
        (string Command, string Description)[] rows =
        [
            .. SettingsCommands.Help,
            .. ProviderCommands.Help,
            .. ModelCommands.Help,
            .. CatalogCommands.Help,
            .. OwnHelp,
        ];

        int width = rows.Max(row => ConsoleResults.Width(row.Command)) + 3;
        foreach ((string command, string description) in rows)
        {
            Console.WriteLine($"{ConsoleResults.PadTo(command, width)}{description}");
        }
    }
}
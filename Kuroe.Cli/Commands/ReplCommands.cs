using System.Text;
using Kuroe.Agent;
using Kuroe.Catalogs;
using Spectre.Console;

namespace Kuroe.Cli.Commands;

/// <summary>启动提示与斜杠命令的分发，各命令族的解析与执行在对应的类型里。</summary>
internal sealed class ReplCommands(
    AgentSession session,
    ModelService models,
    CatalogService catalog,
    ToolCollection tools,
    ProviderCommands providers,
    ModelCommands modelCommands,
    CatalogCommands catalogs,
    SettingsCommands settingsCommands,
    Terminal terminal,
    ConsoleErrors errors)
{
    /// <summary>不属于任何命令族的帮助行。</summary>
    private static readonly (string Command, string Description)[] OwnHelp =
    [
        ("/reset", "清空上下文"),
        ("exit", "退出"),
    ];

    private readonly AgentSession _session = session;
    private readonly ModelService _models = models;
    private readonly CatalogService _catalog = catalog;
    private readonly ToolCollection _tools = tools;
    private readonly ProviderCommands _providers = providers;
    private readonly ModelCommands _modelCommands = modelCommands;
    private readonly CatalogCommands _catalogs = catalogs;
    private readonly SettingsCommands _settingsCommands = settingsCommands;
    private readonly Terminal _terminal = terminal;
    private readonly ConsoleErrors _errors = errors;

    public void Execute(string input)
    {
        string[] parts = Split(input);

        switch (parts[0].ToLowerInvariant())
        {
            case "/help":
                PrintHelp();
                break;

            case "/reset":
                _session.Reset();
                _terminal.Ok("上下文已清空。");
                break;

            case "/config":
                _settingsCommands.PrintConfig();
                break;

            case "/provider":
                _providers.Run(parts);
                break;

            case "/model":
                _modelCommands.Run(parts);
                break;

            case "/catalog":
                _catalogs.Run(parts);
                break;

            case "/set":
                _settingsCommands.Set(parts);
                break;

            case "/unset":
                _settingsCommands.Unset(parts);
                break;

            default:
                _terminal.Warn($"未知命令 {parts[0]}，输入 /help 查看命令。");
                break;
        }
    }

    /// <summary>按空白拆分命令，双引号内的空白不作为分隔符，引号本身不出现在结果中。未闭合的引号按到输入末尾处理。</summary>
    private static string[] Split(string input)
    {
        List<string> parts = [];
        StringBuilder part = new();
        bool quoted = false;
        bool started = false;

        foreach (char character in input)
        {
            if (character == '"')
            {
                quoted = !quoted;
                started = true;
            }
            else if (!quoted && character is ' ' or '\t')
            {
                if (started)
                {
                    parts.Add(part.ToString());
                    part.Clear();
                    started = false;
                }
            }
            else
            {
                part.Append(character);
                started = true;
            }
        }

        if (started)
        {
            parts.Add(part.ToString());
        }

        return [.. parts];
    }

    /// <summary>启动横幅：当前模型、目录规模，以及缺少提供商或工具时的处理指引。</summary>
    public void PrintStartup()
    {
        CatalogSnapshot contents = _catalog.Snapshot();
        string model = _models.Current ?? "未选择";
        _terminal.Line($"Kuroe 已启动，当前模型 {model}，" +
            $"目录中有 {contents.Providers.Count} 个提供商、{contents.Models.Count} 个模型。");
        if (contents.Providers.Count == 0)
        {
            _terminal.Hint("先 /provider add <提供商> <端点> <凭据> 添加提供商，再 /model add <模型> <提供商> 注册模型。");
        }

        if (_tools.Names.Count == 0)
        {
            _terminal.Hint("当前没有可用工具，检查是否注册了工具载体、公开方法是否标注 DescriptionAttribute。");
        }

        _terminal.Hint("输入 exit 退出，/help 查看命令，Ctrl+C 中断当前回复。");
    }

    /// <summary>当前模型无法连接时的注册指引，模型可用时没有输出。</summary>
    public void GuideCurrentModel() => _errors.GuideModelRegistration(_models, _models.Current);

    /// <summary>各命令族的帮助行按固定顺序汇总，命令列与说明列由栅格对齐。</summary>
    private void PrintHelp()
    {
        (string Command, string Description)[] rows =
        [
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

        _terminal.NewLine();
        _terminal.Write(grid);
    }
}

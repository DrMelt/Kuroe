using Kuroe.Catalogs;
using Spectre.Console;

namespace Kuroe.Cli.Commands;

/// <summary>/provider 子命令的解析与执行。</summary>
internal sealed class ProviderCommands(
    CatalogService catalog,
    Terminal terminal,
    ConsoleResults results)
{
    /// <summary>该命令族的帮助行。</summary>
    public static IReadOnlyList<(string Command, string Description)> Help { get; } =
    [
        ("/provider list", "列出提供商与模型"),
        ("/provider add <名> <端点> <凭据>", "新增提供商"),
        ("/provider key <名> <凭据>", "更换提供商凭据"),
        ("/provider rm <名>", "删除提供商，仍被模型引用时拒绝"),
    ];

    private readonly CatalogService _catalog = catalog;
    private readonly Terminal _terminal = terminal;
    private readonly ConsoleResults _results = results;

    public void Run(string[] parts)
    {
        const string usage = "用法：/provider list | add <名> <端点> <凭据> | key <名> <凭据> | rm <名>";
        string subcommand = parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty;

        switch ((subcommand, parts.Length))
        {
            case ("list", 2):
                Print(_catalog.Snapshot());
                break;

            case ("rm", 3):
                Remove(parts[2]);
                break;

            case ("key", 4):
                SetKey(parts[2], parts[3]);
                break;

            case ("add", 5):
                Add(parts[2], parts[3], parts[4]);
                break;

            default:
                _terminal.Hint(usage);
                break;
        }
    }

    private void Add(string name, string baseAddress, string apiKey) =>
        _results.Report(_catalog.AddProvider(name, baseAddress, apiKey), "已保存。");

    private void SetKey(string name, string apiKey) =>
        _results.Report(_catalog.SetProviderKey(name, apiKey), "已保存。");

    private void Remove(string name) =>
        _results.Report(_catalog.RemoveProvider(name), "已删除。");

    /// <summary>列出提供商与模型，凭据只显示是否已设置。</summary>
    private void Print(CatalogSnapshot snapshot)
    {
        if (snapshot.Providers.Count == 0)
        {
            _terminal.Hint("目录为空，用 /provider add <名> <端点> <凭据> 添加提供商。");
            return;
        }

        _terminal.Line("提供商：");
        Grid providers = Terminal.Columns(3, wrapColumns: 1);
        foreach (ProviderInfo provider in snapshot.Providers)
        {
            providers.AddRow(
                new Text(provider.Name, Styles.Key),
                new Text(provider.Endpoint),
                new Text(
                    provider.HasPlaceholderKey ? "凭据是占位符" : "凭据已设置",
                    provider.HasPlaceholderKey ? Styles.Warning : Styles.Success));
        }

        _terminal.Write(providers);

        if (snapshot.Models.Count == 0)
        {
            _terminal.Hint("模型：无，用 /model add <模型> <提供商> 注册。");
            return;
        }

        _terminal.Line("模型：");
        Grid models = Terminal.Columns(2);
        foreach (ModelInfo model in snapshot.Models)
        {
            models.AddRow(
                new Text(model.Name, Styles.Key),
                new Text($"→ {model.ProviderName}"));
        }

        _terminal.Write(models);
    }
}

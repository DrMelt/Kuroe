using Kuroe.Catalogs;

namespace Kuroe.Cli.Commands;

/// <summary>/provider 子命令的解析与执行。</summary>
internal sealed class ProviderCommands(
    CatalogService catalog,
    CatalogPrinter printer,
    Terminal terminal,
    ConsoleResults results)
{
    /// <summary>该命令族的帮助行。</summary>
    public static IReadOnlyList<(string Command, string Description)> Help { get; } =
    [
        ("/provider list", "列出提供商"),
        ("/provider add <名> <端点> <凭据>", "新增提供商"),
        ("/provider key <名> <凭据>", "更换提供商凭据"),
        ("/provider rm <名>", "删除提供商，仍被模型引用时拒绝"),
    ];

    private readonly CatalogService _catalog = catalog;
    private readonly CatalogPrinter _printer = printer;
    private readonly Terminal _terminal = terminal;
    private readonly ConsoleResults _results = results;

    public void Run(string[] parts)
    {
        const string usage = "用法：/provider list | add <名> <端点> <凭据> | key <名> <凭据> | rm <名>";
        string subcommand = parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty;

        switch ((subcommand, parts.Length))
        {
            case ("list", 2):
                _printer.PrintProviders(_catalog.Snapshot().Providers);
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
}

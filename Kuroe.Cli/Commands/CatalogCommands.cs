using ErrorOr;
using Kuroe.Catalogs;

namespace Kuroe.Cli.Commands;

/// <summary>/catalog 子命令的解析与执行。</summary>
internal sealed class CatalogCommands(
    CatalogService catalog,
    Terminal terminal,
    ConsoleResults results)
{
    /// <summary>该命令族的帮助行。</summary>
    public static IReadOnlyList<(string Command, string Description)> Help { get; } =
    [
        ("/catalog export <文件>", "导出目录，凭据替换为占位符"),
        ("/catalog import <文件>", "合并导入目录"),
    ];

    private readonly CatalogService _catalog = catalog;
    private readonly Terminal _terminal = terminal;
    private readonly ConsoleResults _results = results;

    public void Run(string[] parts)
    {
        const string usage = "用法：/catalog export <文件> | import <文件>";
        string subcommand = parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty;

        switch ((subcommand, parts.Length))
        {
            case ("export", 3):
                _results.Report(_catalog.Export(parts[2]), $"已导出，凭据以 {CatalogService.PlaceholderApiKey} 占位。");
                break;

            case ("import", 3):
                Import(parts[2]);
                break;

            default:
                _terminal.Hint(usage);
                break;
        }
    }

    /// <summary>合并导入后列出跳过的条目与仍是占位符的凭据。</summary>
    private void Import(string file)
    {
        ErrorOr<IReadOnlyList<string>> imported = _catalog.Import(file);
        if (imported.IsError)
        {
            _results.Reject(imported.ErrorsOrEmptyList);
            return;
        }

        _terminal.Ok("已合并导入。");
        foreach (string note in imported.Value)
        {
            _terminal.Hint($"  {note}");
        }

        string[] masked = [.. _catalog.Snapshot().Providers
            .Where(provider => CatalogService.IsPlaceholder(provider.ApiKey))
            .Select(provider => provider.ProviderName.Value)];
        if (masked.Length > 0)
        {
            _terminal.Hint($"用 /provider key <提供商> <凭据> 替换占位符凭据：{string.Join('、', masked)}");
        }
    }
}

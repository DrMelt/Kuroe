using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Cli.Views;
using Kuroe.Shared.Catalogs;

namespace Kuroe.Cli.Commands;

/// <summary>/catalog 子命令的解析与执行。</summary>
internal sealed class CatalogCommands(
    CatalogService catalog,
    CatalogPrinter printer,
    Terminal terminal,
    ResultPrinter results)
{
    /// <summary>该命令族的帮助行。</summary>
    public static IReadOnlyList<(string Command, string Description)> Help { get; } =
    [
        ("/catalog list", "列出提供商与模型"),
        ("/catalog export <文件>", "导出目录，凭据替换为占位符"),
        ("/catalog import <文件>", "合并导入目录"),
    ];

    private readonly CatalogService _catalog = catalog;
    private readonly CatalogPrinter _printer = printer;
    private readonly Terminal _terminal = terminal;
    private readonly ResultPrinter _results = results;

    public void Run(string[] parts)
    {
        const string usage = "用法：/catalog list | export <文件> | import <文件>";
        string subcommand = parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty;

        switch ((subcommand, parts.Length))
        {
            case ("list", 2):
                CatalogContents contents = _catalog.Snapshot();
                _printer.PrintProviders(contents.Providers);
                _printer.PrintModels(contents.Models);
                break;

            case ("export", 3):
                Export(parts[2]);
                break;

            case ("import", 3):
                Import(parts[2]);
                break;

            default:
                _terminal.Hint(usage);
                break;
        }
    }

    /// <summary>导出成功后提示写入位置与凭据占位。</summary>
    private void Export(string file)
    {
        ErrorOr<string> exported = _catalog.Export(file);
        if (exported.IsError)
        {
            _results.Reject(exported.ErrorsOrEmptyList);
            return;
        }

        _terminal.Ok($"已导出到 {exported.Value}，凭据已替换为占位符。");
    }

    /// <summary>合并导入后列出跳过的条目与仍是占位符的凭据。</summary>
    private void Import(string file)
    {
        ErrorOr<CatalogMerge> imported = _catalog.Import(file);
        if (imported.IsError)
        {
            _results.Reject(imported.ErrorsOrEmptyList);
            return;
        }

        _terminal.Ok($"已从 {imported.Value.Source} 合并导入。");
        foreach (string note in imported.Value.Notes)
        {
            _terminal.Hint($"  {note}");
        }

        IReadOnlyList<string> masked = imported.Value.PlaceholderProviders;
        if (masked.Count > 0)
        {
            _terminal.Hint($"用 /provider key <提供商> <凭据> 替换占位符凭据：{string.Join('、', masked)}");
        }
    }
}

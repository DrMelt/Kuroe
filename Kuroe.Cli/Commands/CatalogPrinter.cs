using Kuroe.Catalogs;
using Spectre.Console;

namespace Kuroe.Cli.Commands;

/// <summary>目录两张表的终端渲染，供 /provider、/model、/catalog 的 list 共用。</summary>
internal sealed class CatalogPrinter(Terminal terminal)
{
    private readonly Terminal _terminal = terminal;

    /// <summary>列出提供商，凭据只显示是否已设置。</summary>
    public void PrintProviders(IReadOnlyList<ProviderInfo> providers)
    {
        if (providers.Count == 0)
        {
            _terminal.Hint("目录为空，用 /provider add <名> <端点> <凭据> 添加提供商。");
            return;
        }

        _terminal.Line("提供商：");
        Grid grid = Terminal.Columns(3, wrapColumns: 1);
        foreach (ProviderInfo provider in providers)
        {
            grid.AddRow(
                new Text(provider.Name, Styles.Key),
                new Text(provider.Endpoint),
                new Text(
                    provider.HasPlaceholderKey ? "凭据是占位符" : "凭据已设置",
                    provider.HasPlaceholderKey ? Styles.Warning : Styles.Success));
        }

        _terminal.Write(grid);
    }

    /// <summary>列出已注册模型及其提供商。</summary>
    public void PrintModels(IReadOnlyList<ModelInfo> models)
    {
        if (models.Count == 0)
        {
            _terminal.Hint("还没有注册模型，用 /model add <模型> <提供商> 注册。");
            return;
        }

        _terminal.Line("模型：");
        Grid grid = Terminal.Columns(2);
        foreach (ModelInfo model in models)
        {
            grid.AddRow(
                new Text(model.Name, Styles.Key),
                new Text($"→ {model.ProviderName}"));
        }

        _terminal.Write(grid);
    }
}

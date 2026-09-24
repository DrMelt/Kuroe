using ApiHub.Shared.Models;
using Kuroe.Catalogs;
using Spectre.Console;

namespace Kuroe.Cli.Views;

/// <summary>目录两张表的终端渲染，供 /provider、/model、/catalog 的 list 共用。</summary>
internal sealed class CatalogPrinter(Terminal terminal)
{
    private readonly Terminal _terminal = terminal;

    /// <summary>列出提供商，凭据只显示是否已设置。</summary>
    public void PrintProviders(IReadOnlyList<ProviderDefinition> providers)
    {
        if (providers.Count == 0)
        {
            _terminal.Hint("目录为空，用 /provider add <名> <端点> <凭据> 添加提供商。");
            return;
        }

        _terminal.Line("提供商：");
        Grid grid = Terminal.Columns(3, wrapColumns: 1);
        foreach (ProviderDefinition provider in providers)
        {
            bool placeholder = CatalogService.IsPlaceholder(provider.ApiKey);
            grid.AddRow(
                new Text(provider.ProviderName.Value, Styles.Key),
                new Text(provider.BaseAddress.Address.ToString()),
                new Text(
                    placeholder ? "凭据是占位符" : "凭据已设置",
                    placeholder ? Styles.Warning : Styles.Success));
        }

        _terminal.Write(grid);
    }

    /// <summary>列出已注册模型及其提供商。</summary>
    public void PrintModels(IReadOnlyList<ModelDefinition> models)
    {
        if (models.Count == 0)
        {
            _terminal.Hint("还没有注册模型，用 /model add <模型> <提供商> 注册。");
            return;
        }

        _terminal.Line("模型：");
        Grid grid = Terminal.Columns(2);
        foreach (ModelDefinition model in models)
        {
            grid.AddRow(
                new Text(model.ModelName.Value, Styles.Key),
                new Text($"→ {model.ProviderName.Value}"));
        }

        _terminal.Write(grid);
    }
}

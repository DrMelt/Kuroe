using ApiHub.Shared.Models;
using Kuroe.Catalogs;
using Spectre.Console;

namespace Kuroe.Console.Views;

/// <summary>目录两张表的终端渲染，供目录菜单的列表共用。</summary>
internal sealed class CatalogView(Terminal terminal)
{
    private readonly Terminal _terminal = terminal;

    /// <summary>列出提供商，凭据只显示是否已设置。</summary>
    public void PrintProviders(IReadOnlyList<ProviderDefinition> providers)
    {
        if (providers.Count == 0)
        {
            _terminal.Hint("目录为空，在「模型与提供商」菜单添加提供商。");
            return;
        }

        Table table = Blocks.Table(("提供商", false), ("端点", true), ("凭据", false));
        foreach (ProviderDefinition provider in providers)
        {
            bool placeholder = CatalogService.IsPlaceholder(provider.ApiKey);
            table.AddRow(
                new Text(provider.ProviderName.Value, Styles.Key),
                new Text(provider.BaseAddress.Address.ToString()),
                new Text(
                    placeholder ? "凭据是占位符" : "凭据已设置",
                    placeholder ? Styles.Warning : Styles.Success));
        }

        _terminal.Write(Blocks.Section(BoxKind.View, $"提供商 {providers.Count} 个", table));
    }

    /// <summary>列出已注册模型及其提供商，标记当前模型。</summary>
    public void PrintModels(IReadOnlyList<ModelDefinition> models, ModelName? current)
    {
        if (models.Count == 0)
        {
            _terminal.Hint("还没有注册模型，在「模型与提供商」菜单注册。");
            return;
        }

        Table table = Blocks.Table(("模型", false), ("提供商", true), ("当前", false));
        foreach (ModelDefinition model in models)
        {
            bool selected = current is { } name && model.ModelName.Equals(name);
            table.AddRow(
                new Text(model.ModelName.Value, Styles.Key),
                new Text(model.ProviderName.Value),
                new Text(selected ? "当前" : string.Empty, Styles.Success));
        }

        _terminal.Write(Blocks.Section(BoxKind.View, $"模型 {models.Count} 个", table));
    }
}

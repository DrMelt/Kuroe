using ApiHub.Shared.Models;
using Kuroe.Catalogs;

namespace Kuroe.Cli.Views;

/// <summary>提供商与模型列表的纯文本呈现。</summary>
internal static class PlainCatalogView
{
    /// <summary>列出提供商，凭据只显示是否已设置。</summary>
    public static void Providers(CliOutput output, IReadOnlyList<ProviderDefinition> providers)
    {
        if (providers.Count == 0)
        {
            output.Hint("提供商目录为空，用 provider add <名> <端点> <凭据> 添加提供商。");

            return;
        }

        output.Line("提供商\t端点\t凭据");
        foreach (ProviderDefinition provider in providers)
        {
            output.Line($"{provider.ProviderName.Value}\t{provider.BaseAddress.Address}\t{(CatalogService.IsPlaceholder(provider.ApiKey) ? "占位符" : "已设置")}");
        }
    }

    /// <summary>列出模型并标记当前模型。</summary>
    public static void Models(CliOutput output, IReadOnlyList<ModelDefinition> models, ModelName? current)
    {
        if (models.Count == 0)
        {
            output.Hint("还没有注册模型，用 model add <模型> <提供商> 注册。");

            return;
        }

        output.Line("模型\t提供商\t当前");
        foreach (ModelDefinition model in models)
        {
            bool selected = current is { } name && model.ModelName.Equals(name);
            output.Line($"{model.ModelName.Value}\t{model.ProviderName.Value}\t{(selected ? "当前" : string.Empty)}");
        }
    }
}
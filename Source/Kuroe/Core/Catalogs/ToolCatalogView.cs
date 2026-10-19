using ApiHub.Shared.Models;
using Kuroe.Shared.Catalogs;

namespace Kuroe.Catalogs;

/// <summary>目录工具用的只读视图：一次读取目录快照得出提供商与模型清单，当前模型取自选择，凭据只报是否已设置。</summary>
internal sealed class ToolCatalogView(CatalogService catalog, ModelService models) : ICatalogView
{
    /// <summary>目录概况：清单取自同一份快照。</summary>
    public CatalogSummary Read()
    {
        CatalogContents snapshot = catalog.Snapshot();

        return new CatalogSummary(
            [
                .. snapshot.Providers.Select(provider => new ProviderInfo(
                    provider.ProviderName.Value,
                    provider.BaseAddress.Address.ToString(),
                    !CatalogService.IsPlaceholder(provider.ApiKey))),
            ],
            [.. snapshot.Models.Select(model => new ModelInfo(model.ModelName.Value, model.ProviderName.Value))],
            models.Current?.Value);
    }
}
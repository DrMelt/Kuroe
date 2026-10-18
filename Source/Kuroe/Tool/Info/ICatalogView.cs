namespace Kuroe.Tool.Info;

/// <summary>模型目录的只读视图：一次读取给出提供商、模型清单与当前选中模型。由宿主提供，凭据只报是否已设置。</summary>
public interface ICatalogView
{
    /// <summary>模型的目录概况。</summary>
    CatalogSummary Read();
}

/// <summary>模型目录的一次读取结果，三个字段取自同一份快照。</summary>
/// <param name="Providers">提供商清单。</param>
/// <param name="Models">模型清单。</param>
/// <param name="CurrentModel">当前选中的模型名，未选择时为空。</param>
public sealed record CatalogSummary(IReadOnlyList<ProviderInfo> Providers, IReadOnlyList<ModelInfo> Models, string? CurrentModel);

/// <summary>目录里的一个提供商。</summary>
/// <param name="Name">提供商名。</param>
/// <param name="Endpoint">端点地址。</param>
/// <param name="CredentialSet">凭据已设置；导出时的占位文本不算已设置。</param>
public sealed record ProviderInfo(string Name, string Endpoint, bool CredentialSet);

/// <summary>目录里的一个模型。</summary>
/// <param name="Name">模型名。</param>
/// <param name="Provider">所属提供商名。</param>
public sealed record ModelInfo(string Name, string Provider);
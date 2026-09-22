namespace Kuroe.Catalogs;

/// <summary>目录当前快照，交出后不受目录后续改动影响。</summary>
public sealed record CatalogSnapshot(IReadOnlyList<ProviderInfo> Providers, IReadOnlyList<ModelInfo> Models);

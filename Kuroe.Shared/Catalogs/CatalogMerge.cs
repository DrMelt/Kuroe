namespace Kuroe.Shared.Catalogs;

/// <summary>一次合并导入的结果：来源文件的绝对路径、逐条说明，以及导入后凭据仍为占位符的提供商。</summary>
public sealed record CatalogMerge(
    string Source,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string> PlaceholderProviders);

namespace Kuroe.Catalogs;

/// <summary>一次合并导入的结果：来源文件的绝对路径与逐条说明。</summary>
public sealed record CatalogMerge(string Source, IReadOnlyList<string> Notes);

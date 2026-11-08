namespace Kuroe.Tool.FileAccess;

/// <summary>文本搜索的结果：匹配与扫描计数。</summary>
/// <param name="Matches">匹配集合，按文件与行号排序。</param>
/// <param name="ScannedFiles">扫描的文件数，包含二进制判定跳过与读取失败的文件。</param>
public sealed record TextSearchResult(
    IReadOnlyList<TextSearchMatch> Matches,
    int ScannedFiles);
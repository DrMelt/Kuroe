namespace Kuroe.Tool.FileAccess;

/// <summary>文本搜索的结果：匹配与扫描、跳过计数。</summary>
/// <param name="Matches">匹配集合，按文件与行号排序。</param>
/// <param name="ScannedFiles">读取过的文件数。</param>
/// <param name="SkippedFiles">因二进制或读取失败跳过的文件数，是扫描文件数的一部分。</param>
public sealed record TextSearchResult(
    IReadOnlyList<TextSearchMatch> Matches,
    int ScannedFiles,
    int SkippedFiles);
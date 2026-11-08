namespace Kuroe.Tool.FileAccess.Shared;

/// <summary>文本搜索的结果：匹配行、扫描计数与出现次数。</summary>
/// <param name="Matches">匹配行集合，按文件与行号排序，同一行只保留一条。</param>
/// <param name="ScannedFiles">扫描的文件数，包含二进制判定跳过与读取失败的文件。</param>
/// <param name="Occurrences">已收集匹配行中的非重叠出现次数，同一行多处按多次计。</param>
public sealed record TextSearchResult(
    IReadOnlyList<TextSearchMatch> Matches,
    int ScannedFiles,
    int Occurrences);
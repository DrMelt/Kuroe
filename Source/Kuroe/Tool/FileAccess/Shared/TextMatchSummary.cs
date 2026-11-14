namespace Kuroe.Tool.FileAccess.Shared;

/// <summary>文本的匹配统计：首个匹配起点与不重叠出现次数。</summary>
/// <param name="Index">首个匹配起点，从 0 起，没有匹配时为 -1。</param>
/// <param name="Occurrences">不重叠出现次数，零宽匹配按每次出现计数。</param>
public readonly record struct TextMatchSummary(int Index, int Occurrences);
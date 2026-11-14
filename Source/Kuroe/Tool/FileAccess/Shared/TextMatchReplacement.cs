namespace Kuroe.Tool.FileAccess.Shared;

/// <summary>一处匹配与按替换模板展开后的文本。</summary>
/// <param name="Index">匹配起点，从 0 起。</param>
/// <param name="Length">匹配长度，零宽匹配为 0。</param>
/// <param name="Text">展开后的替换文本。</param>
public sealed record TextMatchReplacement(int Index, int Length, string Text);
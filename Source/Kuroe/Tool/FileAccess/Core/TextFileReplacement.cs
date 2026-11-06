namespace Kuroe.Tool.FileAccess;

/// <summary>一次文本替换在单个文件上的结果：替换后的原文、替换处数与匹配起始行号。</summary>
/// <param name="Text">替换后的原文，未匹配内容保持原样。</param>
/// <param name="Replacements">替换处数，没有匹配时为 0。</param>
/// <param name="Lines">匹配起始行号，从 1 起，同一行多处只留一个行号。</param>
public sealed record TextFileReplacement(
    string Text,
    int Replacements,
    IReadOnlyList<int> Lines);
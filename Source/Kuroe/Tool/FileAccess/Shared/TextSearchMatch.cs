namespace Kuroe.Tool.FileAccess.Shared;

/// <summary>文本搜索的一处匹配：文件路径、行号、行内展示窗口与行长。</summary>
/// <param name="File">匹配文件的完整路径。</param>
/// <param name="Line">行号，从 1 起。</param>
/// <param name="Text">展示的行内容窗口，不含终止符，长度不超过 <see cref="DisplayLimit"/>。</param>
/// <param name="ContentLength">行内容的完整字符数，不含终止符。</param>
/// <param name="TerminatorLength">该行终止符的字符数。</param>
/// <param name="Offset">展示内容在行内的起始位置，从 0 起。</param>
public sealed record TextSearchMatch(
    string File,
    int Line,
    string Text,
    int ContentLength,
    int TerminatorLength,
    int Offset)
{
    /// <summary>展示内容的字符上限，只影响展示，不影响匹配。</summary>
    public const int DisplayLimit = 1000;

    /// <summary>完整行长，含内容与终止符。</summary>
    public int LineLength => ContentLength + TerminatorLength;

    /// <summary>展示内容是否短于整行内容。</summary>
    public bool Truncated => Offset > 0 || ContentLength > Offset + Text.Length;

    /// <summary>按命中位置截取展示窗口，行内容不超过上限时保留整行，窗口边界不切断代理对。</summary>
    /// <param name="file">匹配文件的完整路径。</param>
    /// <param name="lineNumber">行号，从 1 起。</param>
    /// <param name="line">行内容，不含终止符。</param>
    /// <param name="terminatorLength">该行终止符的字符数。</param>
    /// <param name="matchIndex">命中起点在行内的位置。</param>
    public static TextSearchMatch Create(
        string file,
        int lineNumber,
        string line,
        int terminatorLength,
        int matchIndex)
    {
        if (line.Length <= DisplayLimit)
        {
            return new TextSearchMatch(file, lineNumber, line, line.Length, terminatorLength, 0);
        }

        int start = Math.Clamp(matchIndex - DisplayLimit / 2, 0, line.Length - DisplayLimit);
        if (start > 0 && char.IsLowSurrogate(line[start]))
        {
            start--;
        }

        int end = Math.Min(line.Length, start + DisplayLimit);
        if (end < line.Length && char.IsLowSurrogate(line[end]))
        {
            end--;
        }

        return new TextSearchMatch(
            file,
            lineNumber,
            line[start..end],
            line.Length,
            terminatorLength,
            start);
    }
}
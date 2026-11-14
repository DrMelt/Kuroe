namespace Kuroe.Presentation.Shared;

/// <summary>过程记录文本的展示处理：统一换行符并按逻辑行切分。</summary>
public static class JournalText
{
    /// <summary>把 CRLF 与 CR 归一为 LF。</summary>
    public static string Normalize(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n');

    /// <summary>按 LF 切分为逻辑行，末尾终止符不产生额外的空行。</summary>
    public static string[] Lines(string text)
    {
        string[] lines = Normalize(text).Split('\n');
        return lines.Length > 1 && lines[^1].Length == 0 ? lines[..^1] : lines;
    }
}
namespace Kuroe.Tool.FileAccess;

/// <summary>换行处理：把 CRLF、CR 与 LF 归一为 LF，识别文本首处换行风格，以及把 LF 转为指定换行风格。</summary>
public static class TextNewLines
{
    /// <summary>把 CRLF 与 CR 归一为 LF。</summary>
    public static string Normalize(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    /// <summary>识别文本首处换行风格，没有换行时返回 LF。</summary>
    public static string Detect(string text)
    {
        int index = text.IndexOfAny(['\r', '\n']);
        if (index < 0)
        {
            return "\n";
        }

        if (text[index] != '\r')
        {
            return "\n";
        }

        return index + 1 < text.Length && text[index + 1] == '\n' ? "\r\n" : "\r";
    }

    /// <summary>把文本中的 LF 转为指定换行风格。</summary>
    public static string Convert(string text, string newLine) =>
        newLine == "\n" ? text : text.Replace("\n", newLine, StringComparison.Ordinal);
}
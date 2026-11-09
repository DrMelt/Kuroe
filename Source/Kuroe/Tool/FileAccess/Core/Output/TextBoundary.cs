namespace Kuroe.Tool.FileAccess.Output;

/// <summary>文本前缀截取：按 UTF-16 码元计数，不切开代理对，不留下单独的 CR。</summary>
public static class TextBoundary
{
    /// <summary>取前 limit 个字符。末尾是 CR 时丢弃该 CR，是高代理项时丢弃该代理项。limit 非正时返回空串。</summary>
    public static string Take(string text, int limit)
    {
        if (limit <= 0)
        {
            return string.Empty;
        }

        if (text.Length <= limit)
        {
            return text;
        }

        int length = limit;
        if (text[length - 1] == '\r')
        {
            length--;
        }

        if (length > 0 && char.IsHighSurrogate(text[length - 1]))
        {
            length--;
        }

        return text[..length];
    }
}
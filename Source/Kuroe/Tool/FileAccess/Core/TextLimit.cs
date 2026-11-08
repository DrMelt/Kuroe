namespace Kuroe.Tool.FileAccess;

/// <summary>工具输出的字符上限与截断：超限时截断正文，提示放在正文前。</summary>
public static class TextLimit
{
    /// <summary>工具输出的字符上限。</summary>
    public const int Characters = 8000;

    /// <summary>正文超过上限时截断。截断点前一个字符是 CR 时丢弃该 CR，是高代理项时丢弃该代理项。</summary>
    public static string Body(string content, out bool truncated)
    {
        truncated = content.Length > Characters;
        if (!truncated)
        {
            return content;
        }

        int length = Characters;
        if (content[length - 1] == '\r')
        {
            length--;
        }

        if (char.IsHighSurrogate(content[length - 1]))
        {
            length--;
        }

        return content[..length];
    }

    /// <summary>正文超过上限时截断，并把截断提示放在正文前。</summary>
    public static string WithNotice(string content, out bool truncated)
    {
        string body = Body(content, out truncated);
        return truncated ? $"{Notice}\n{body}" : body;
    }

    /// <summary>正文超过上限时正文前的截断提示。</summary>
    public static string Notice => $"（内容超过 {Characters} 字符，已截断）";
}
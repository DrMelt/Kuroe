namespace Kuroe.Tool.FileAccess.Output;

/// <summary>带字符上限的文本：正文与截断后的正文。</summary>
/// <param name="Content">截断前的正文。</param>
public readonly record struct LimitedText(string Content)
{
    /// <summary>正文是否超过字符上限。</summary>
    public bool Truncated => Content.Length > TextOutputLimit.Characters;

    /// <summary>截断后的正文。截断点前一个字符是 CR 时丢弃该 CR，是高代理项时丢弃该代理项。</summary>
    public string Body
    {
        get
        {
            if (!Truncated)
            {
                return Content;
            }

            int length = TextOutputLimit.Characters;
            if (Content[length - 1] == '\r')
            {
                length--;
            }

            if (length > 0 && char.IsHighSurrogate(Content[length - 1]))
            {
                length--;
            }

            return Content[..length];
        }
    }

    /// <summary>正文超过上限时正文前的截断提示。</summary>
    public static string Notice => $"（内容超过 {TextOutputLimit.Characters} 字符，已截断）";

    /// <summary>正文与可选截断提示，提示放在正文前。</summary>
    public override string ToString() =>
        Truncated ? new NoticeText(Notice, Body).ToString() : Body;
}
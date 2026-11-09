namespace Kuroe.Tool.FileAccess.Output;

/// <summary>带字符上限的文本：正文与截断后的正文。</summary>
/// <param name="Content">截断前的正文。</param>
/// <param name="Limit">正文字符上限。</param>
public readonly record struct LimitedText(string Content, int Limit)
{
    /// <summary>正文是否超过字符上限。</summary>
    public bool Truncated => Content.Length > Limit;

    /// <summary>截断后的正文。截断不切开 CRLF 与代理对。</summary>
    public string Body => Truncated ? TextBoundary.Take(Content, Limit) : Content;

    /// <summary>正文超过上限时正文前的截断提示。</summary>
    public string Notice => $"（内容超过 {Limit} 字符，已截断）";

    /// <summary>正文与可选截断提示，提示放在正文前。</summary>
    public override string ToString() =>
        Truncated ? new NoticeText(Notice, Body).ToString() : Body;
}
namespace Kuroe.Tool.FileAccess.Output;

/// <summary>带前缀提示的文本：提示与正文。</summary>
/// <param name="Notice">正文前的提示，空串表示没有提示。</param>
/// <param name="Body">提示之后的正文。</param>
public readonly record struct NoticeText(string Notice, string Body)
{
    /// <summary>提示与正文，提示非空时以 LF 分隔。</summary>
    public override string ToString() => Notice.Length == 0 ? Body : $"{Notice}\n{Body}";
}
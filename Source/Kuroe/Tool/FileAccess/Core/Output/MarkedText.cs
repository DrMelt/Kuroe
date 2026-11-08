namespace Kuroe.Tool.FileAccess.Output;

/// <summary>带固定标记的文本：正文以及包住它的开始标记与结束标记。</summary>
/// <param name="Content">不含标记的正文。</param>
public readonly record struct MarkedText(string Content)
{
    /// <summary>开始标记。</summary>
    public const string Begin = "<<<KUROE_TEXT_BEGIN>>>";

    /// <summary>结束标记。</summary>
    public const string End = "<<<KUROE_TEXT_END>>>";

    /// <summary>带标记的文本，结束标记后不追加换行。</summary>
    public override string ToString() => $"{Begin}{Content}{End}";
}
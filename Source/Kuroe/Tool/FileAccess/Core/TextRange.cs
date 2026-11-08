namespace Kuroe.Tool.FileAccess;

/// <summary>读取输出的文本范围标记。</summary>
public static class TextRange
{
    /// <summary>文本范围开始标记。</summary>
    public const string Begin = "<<<KUROE_TEXT_BEGIN>>>";

    /// <summary>文本范围结束标记。</summary>
    public const string End = "<<<KUROE_TEXT_END>>>";

    /// <summary>把正文包在范围标记内，结束标记后不追加换行。</summary>
    public static string Wrap(string body) => $"{Begin}{body}{End}";
}
namespace Kuroe.Tool.FileAccess.Shared;

/// <summary>换行风格：LF、CRLF 或 CR 的文本表示，以及换行归一、识别与转换。</summary>
/// <param name="Value">换行符文本。</param>
public readonly record struct NewLineStyle(string Value)
{
    /// <summary>LF 换行。</summary>
    public static NewLineStyle Lf { get; } = new("\n");

    /// <summary>CRLF 换行。</summary>
    public static NewLineStyle CrLf { get; } = new("\r\n");

    /// <summary>CR 换行。</summary>
    public static NewLineStyle Cr { get; } = new("\r");

    /// <summary>把 CRLF 与 CR 归一为 LF。</summary>
    public static string Normalize(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    /// <summary>识别文本首处换行风格，没有换行时返回 LF。</summary>
    public static NewLineStyle Detect(string text)
    {
        int index = text.IndexOfAny(['\r', '\n']);
        if (index < 0)
        {
            return Lf;
        }

        if (text[index] != '\r')
        {
            return Lf;
        }

        return index + 1 < text.Length && text[index + 1] == '\n' ? CrLf : Cr;
    }

    /// <summary>把文本中的 LF 转为当前换行风格。</summary>
    public string Convert(string text) =>
        Value == "\n" ? text : text.Replace("\n", Value, StringComparison.Ordinal);

    /// <summary>换行符文本。</summary>
    public override string ToString() => Value;
}
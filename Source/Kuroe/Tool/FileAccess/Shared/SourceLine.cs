namespace Kuroe.Tool.FileAccess.Shared;

/// <summary>从文件读取的一行：原始行号、保留的行内容与该行在文件中的终止符。</summary>
/// <param name="Number">行号，从 1 起。</param>
/// <param name="Text">保留的行内容，不含终止符。</param>
/// <param name="Terminator">保留的行尾终止符，null 表示未保留终止符。</param>
public readonly record struct SourceLine(int Number, string Text, NewLineStyle? Terminator)
{
    /// <summary>截断前的原始行长，含终止符；未截断时为 null。</summary>
    public int? OriginalLineLength { get; init; }

    /// <summary>该行是否因单行上限被截断。截断行不保留终止符。</summary>
    public bool Truncated => OriginalLineLength is not null;

    /// <summary>原始行长，含终止符。未截断时由保留内容与终止符计算。</summary>
    public int LineLength =>
        OriginalLineLength ?? Text.Length + (Terminator?.Value.Length ?? 0);
}
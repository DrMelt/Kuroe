namespace Kuroe.Tool.FileAccess;

/// <summary>从文件读取的一行：原始行号、行内容与该行在文件中的终止符。</summary>
/// <param name="Number">行号，从 1 起。</param>
/// <param name="Text">行内容，不含终止符。</param>
/// <param name="NewLine">行尾终止符：CRLF、CR、LF 或空串，空串表示文件末行没有终止符。</param>
public readonly record struct SourceLine(int Number, string Text, string NewLine);
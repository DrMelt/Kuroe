namespace Kuroe.Tool.FileAccess;

/// <summary>带原始行号的一行文本，用于按行号渲染。</summary>
/// <param name="Number">行号，从 1 起。</param>
/// <param name="Text">行内容。</param>
public readonly record struct NumberedLine(int Number, string Text);
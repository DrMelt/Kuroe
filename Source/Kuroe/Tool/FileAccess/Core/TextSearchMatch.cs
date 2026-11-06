namespace Kuroe.Tool.FileAccess;

/// <summary>文本搜索的一处匹配：完整文件路径、行号与行内容。</summary>
/// <param name="File">匹配文件的完整路径。</param>
/// <param name="Line">行号，从 1 起。</param>
/// <param name="Text">行内容，不含换行符；单行超过输出上限时只保留前 8000 字符并以省略号标记。</param>
public sealed record TextSearchMatch(string File, int Line, string Text);
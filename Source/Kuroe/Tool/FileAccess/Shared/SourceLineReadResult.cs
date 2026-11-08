namespace Kuroe.Tool.FileAccess.Shared;

/// <summary>按行号范围读取文件的结果。</summary>
/// <param name="Lines">读取到的行，行号连续。起始行超出文件长度时没有行。</param>
/// <param name="ToBeyondEnd">请求的结束行超出文件尾。字符预算先触发时为假。</param>
public readonly record struct SourceLineReadResult(
    IReadOnlyList<SourceLine> Lines,
    bool ToBeyondEnd);
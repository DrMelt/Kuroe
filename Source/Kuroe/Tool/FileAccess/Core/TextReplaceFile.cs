namespace Kuroe.Tool.FileAccess;

/// <summary>批量替换中一个文件的结果：完整路径、替换处数、匹配起始行或失败原因。</summary>
/// <param name="File">文件的完整路径。</param>
/// <param name="Replacements">替换处数，失败时为 0。</param>
/// <param name="Lines">匹配起始行号，从 1 起，同一行多处只留一个行号。</param>
/// <param name="Error">失败原因，成功时为 null。</param>
public sealed record TextReplaceFile(
    string File,
    int Replacements,
    IReadOnlyList<int> Lines,
    string? Error);
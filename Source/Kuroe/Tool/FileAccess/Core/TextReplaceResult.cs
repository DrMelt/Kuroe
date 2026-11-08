namespace Kuroe.Tool.FileAccess;

/// <summary>批量替换的结果：命中替换或失败的文件与读取过的文件数。</summary>
/// <param name="Files">命中替换或失败的文件，按完整路径序数排序；dryRun 时未写入。</param>
/// <param name="ScannedFiles">读取过的文件数，包含二进制与读取失败的文件。</param>
public sealed record TextReplaceResult(
    IReadOnlyList<TextReplaceFile> Files,
    int ScannedFiles)
{
    /// <summary>成功替换的文件数。</summary>
    public int ChangedFiles => Files.Count(file => file.Error is null);

    /// <summary>成功替换的总处数。</summary>
    public int Replacements => Files.Where(file => file.Error is null).Sum(file => file.Replacements);
}
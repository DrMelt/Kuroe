using System.Text;
using Kuroe.Tool.FileAccess.Output;
using Kuroe.Tool.FileAccess.Shared;

namespace Kuroe.Tool.File.Output;

/// <summary>批量替换报告文本：总数、失败项与逐文件明细，整体受输出上限约束。</summary>
/// <param name="Result">替换结果。</param>
/// <param name="Paths">替换范围的展示路径。</param>
/// <param name="DryRun">是否只报告不写入。</param>
/// <param name="Root">相对路径的基准目录。</param>
public readonly record struct ReplaceReportText(
    TextReplaceResult Result,
    IReadOnlyList<string> Paths,
    bool DryRun,
    string Root)
{
    /// <summary>替换报告文本，整体超过输出上限时截断并提示。</summary>
    public override string ToString()
    {
        string scope = string.Join('、', Paths);
        if (Result.Files.Count == 0)
        {
            return $"在 {scope} 中扫描 {Result.ScannedFiles} 个文件，没有匹配，未修改文件。";
        }

        string action = DryRun ? "将替换" : "已替换";
        var text = new StringBuilder();
        text.Append($"在 {scope} 中扫描 {Result.ScannedFiles} 个文件，{action} {Result.ChangedFiles} 个文件的 {Result.Replacements} 处文本。");

        TextReplaceFile[] changed = [.. Result.Files.Where(file => file.Error is null)];
        TextReplaceFile[] failed = [.. Result.Files.Where(file => file.Error is not null)];

        if (failed.Length > 0)
        {
            text.Append($"\n失败 {failed.Length} 个文件：");
            foreach (TextReplaceFile file in failed)
            {
                text.Append($"\n{DisplayPath(file.File)}：{file.Error}");
            }
        }

        if (changed.Length > 0)
        {
            text.Append(DryRun ? "\n将修改：" : "\n已修改：");
            foreach (TextReplaceFile file in changed)
            {
                text.Append($"\n{DisplayPath(file.File)}：{file.Replacements} 处，第 {string.Join("、", file.Lines)} 行");
            }
        }

        return new LimitedText(text.ToString()).ToString();
    }

    /// <summary>文件的展示路径，相对路径基准目录。</summary>
    private string DisplayPath(string file) => Path.GetRelativePath(Root, file);
}
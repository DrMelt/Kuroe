using Kuroe.Tool.FileAccess.Output;
using Kuroe.Tool.FileAccess.Shared;
using Kuroe.Tool.File.Output;
using Xunit;

namespace Kuroe.Tool.File.Tests.Output;

/// <summary>替换报告文本：无匹配、失败项、修改项与 dryRun 文案。</summary>
public sealed class ReplaceReportTextTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "kuroe-tests", "root");

    [Fact]
    public void ToString_reports_no_match()
    {
        TextReplaceResult result = new([], ScannedFiles: 3);

        string text = new ReplaceReportText(result, ["."], DryRun: false, Root).ToString();

        Assert.Equal("在 . 中扫描 3 个文件，没有匹配，未修改文件。", text);
    }

    [Fact]
    public void ToString_reports_failed_and_changed_files()
    {
        TextReplaceFile changed = new(Path.Combine(Root, "a.txt"), 2, [1, 2], null);
        TextReplaceFile failed = new(Path.Combine(Root, "b.txt"), 0, [], "写入失败");
        TextReplaceResult result = new([changed, failed], ScannedFiles: 5);

        string text = new ReplaceReportText(result, ["."], DryRun: false, Root).ToString();

        Assert.Contains("扫描 5 个文件，已替换 1 个文件的 2 处文本。", text, StringComparison.Ordinal);
        Assert.Contains("失败 1 个文件：\nb.txt：写入失败", text, StringComparison.Ordinal);
        Assert.Contains("已修改：\na.txt：2 处，第 1、2 行", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_uses_the_dry_run_wording()
    {
        TextReplaceFile changed = new(Path.Combine(Root, "a.txt"), 1, [1], null);
        TextReplaceResult result = new([changed], ScannedFiles: 1);

        string text = new ReplaceReportText(result, ["."], DryRun: true, Root).ToString();

        Assert.Contains("将替换 1 个文件的 1 处文本。", text, StringComparison.Ordinal);
        Assert.Contains("将修改：\na.txt：1 处，第 1 行", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_marks_truncation()
    {
        TextReplaceFile failed = new(
            Path.Combine(Root, "a.txt"),
            0,
            [],
            new string('x', TextOutputLimit.Characters + 1000));
        TextReplaceResult result = new([failed], ScannedFiles: 1);

        string text = new ReplaceReportText(result, ["."], DryRun: false, Root).ToString();

        Assert.StartsWith($"（内容超过 {TextOutputLimit.Characters} 字符，已截断）\n", text, StringComparison.Ordinal);
    }
}
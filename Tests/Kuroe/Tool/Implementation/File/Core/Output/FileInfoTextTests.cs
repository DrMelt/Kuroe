using Kuroe.Files.Shared;
using Kuroe.Tool.File.Output;
using Xunit;

namespace Kuroe.Tool.File.Tests.Output;

/// <summary>文件信息文本：大小、时间与属性。</summary>
public sealed class FileInfoTextTests
{
    private static readonly DateTimeOffset At = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public void ToString_renders_size_times_and_attributes()
    {
        FileFacts facts = new(42, At, At, At, FileAttributes.Archive);

        string text = new FileInfoText("notes.txt", facts).ToString();

        Assert.Equal(
            string.Join('\n',
                "notes.txt",
                "大小：42 字节",
                "创建时间：2026-01-02 03:04:05",
                "修改时间：2026-01-02 03:04:05",
                "访问时间：2026-01-02 03:04:05",
                "属性：归档"),
            text);
    }

    [Fact]
    public void ToString_lists_multiple_attributes_in_a_fixed_order()
    {
        FileFacts facts = new(1, At, At, At, FileAttributes.Hidden | FileAttributes.ReadOnly);

        string text = new FileInfoText("notes.txt", facts).ToString();

        Assert.Contains("属性：只读、隐藏", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_reports_no_attributes()
    {
        FileFacts facts = new(1, At, At, At, FileAttributes.Normal);

        string text = new FileInfoText("notes.txt", facts).ToString();

        Assert.Contains("属性：无", text, StringComparison.Ordinal);
    }
}
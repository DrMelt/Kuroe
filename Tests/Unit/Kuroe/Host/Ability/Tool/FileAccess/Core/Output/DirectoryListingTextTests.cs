using Kuroe.Host.Ability.Tool.FileAccess.Output;
using Kuroe.Host.Ability.Tool.FileAccess.Shared;
using Xunit;

namespace Kuroe.Host.Ability.Tool.FileAccess.Tests.Output;

/// <summary>目录列表文本：条目、目录标记、空目录与条目上限。</summary>
public sealed class DirectoryListingTextTests
{
    [Fact(DisplayName = "目录清单文本列出条目并标记目录")]
    public void ToString_lists_entries_and_marks_directories()
    {
        DirectoryEntry[] entries = [new("a.txt", false), new("b", true)];

        string text = new DirectoryListingText("工作目录根", entries).ToString();

        Assert.Equal(
            string.Join('\n', "工作目录根 的条目（2）：", "  a.txt", "  b/"),
            text);
    }

    [Fact(DisplayName = "目录清单文本报告空目录")]
    public void ToString_reports_an_empty_directory()
    {
        string text = new DirectoryListingText("工作目录根", []).ToString();

        Assert.Equal("目录 工作目录根 为空。", text);
    }

    [Fact(DisplayName = "目录清单文本按条目上限截断")]
    public void ToString_truncates_beyond_the_entry_limit()
    {
        DirectoryEntry[] entries =
        [
            .. Enumerable.Range(1, DirectoryListingText.MaxEntries + 1)
                .Select(index => new DirectoryEntry($"File{index:000}.cs", false)),
        ];

        string text = new DirectoryListingText("工作目录根", entries).ToString();

        Assert.Contains($"工作目录根 的条目（{DirectoryListingText.MaxEntries + 1}）：", text, StringComparison.Ordinal);
        Assert.Contains($"…仅列出前 {DirectoryListingText.MaxEntries} 条", text, StringComparison.Ordinal);
        Assert.Contains("File200.cs", text, StringComparison.Ordinal);
        Assert.DoesNotContain("File201.cs", text, StringComparison.Ordinal);
    }
}
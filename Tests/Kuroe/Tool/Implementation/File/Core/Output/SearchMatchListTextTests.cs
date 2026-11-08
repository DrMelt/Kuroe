using Kuroe.Tool.FileAccess.Shared;
using Kuroe.Tool.File.Output;
using Xunit;

namespace Kuroe.Tool.File.Tests.Output;

/// <summary>搜索匹配列表文本：文件分组、相对路径、行号与展示窗口。</summary>
public sealed class SearchMatchListTextTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "kuroe-tests", "root");

    [Fact]
    public void ToString_groups_matches_by_file_with_numbered_lines()
    {
        TextSearchMatch[] matches =
        [
            new(Path.Combine(Root, "a.txt"), 3, "needle", 6, 0),
            new(Path.Combine(Root, "sub", "b.txt"), 12, "needle", 6, 0),
        ];

        string text = new SearchMatchListText(matches, "在 路径基准根 中匹配 2 处：", Root).ToString();

        string expected = string.Join('\n',
            "在 路径基准根 中匹配 2 处：",
            "a.txt",
            "3 |needle",
            Path.Combine("sub", "b.txt"),
            "12 |needle");
        Assert.Equal(expected, text);
    }

    [Fact]
    public void ToString_marks_a_truncated_display_window()
    {
        TextSearchMatch[] matches = [new(Path.Combine(Root, "a.txt"), 1, "needle", 1001, 0)];

        string text = new SearchMatchListText(matches, "匹配：", Root).ToString();

        Assert.EndsWith(
            "1 |needle（本行共 1001 字符，展示第 1 至 6 字符）",
            text,
            StringComparison.Ordinal);
    }
}
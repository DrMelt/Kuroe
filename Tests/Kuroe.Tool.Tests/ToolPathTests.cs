using Kuroe.Tool;
using Xunit;

namespace Kuroe.Tool.Tests;

/// <summary>工具路径：按函数名与可选分组派生出完整路径，白名单条目按前缀放行，路径格式在入口校验。</summary>
public sealed class ToolPathTests
{
    [Fact]
    public void Path_without_group_is_the_function_name()
    {
        ToolPath path = ToolPath.Of(new ToolName("GetTime"));

        Assert.Equal(new ToolPath("GetTime"), path);
        Assert.Equal("GetTime", path.ToString());
    }

    [Fact]
    public void Path_with_group_prefixes_the_function_name()
    {
        ToolPath path = ToolPath.Of(new ToolName("ReadFile"), new ToolPath("files"));

        Assert.Equal(new ToolPath("files/ReadFile"), path);
    }

    [Fact]
    public void Blank_group_is_the_same_as_no_group() =>
        Assert.Equal(new ToolPath("GetTime"), ToolPath.Of(new ToolName("GetTime"), new ToolPath(string.Empty)));

    [Theory]
    [InlineData("info", "info")]
    [InlineData("info", "info/GetTask")]
    [InlineData("info/GetTask", "info/GetTask")]
    public void Allows_accepts_the_entry_and_its_subtree(string entry, string path) =>
        Assert.True(new ToolPath(entry).Allows(path));

    [Theory]
    [InlineData("info", "infoX")]
    [InlineData("info/GetTask", "info")]
    [InlineData("Info", "info")]
    public void Allows_rejects_paths_outside_the_entry(string entry, string path) =>
        Assert.False(new ToolPath(entry).Allows(path));

    [Fact]
    public void Empty_entry_allows_nothing()
    {
        ToolPath entry = new(string.Empty);

        Assert.False(entry.Allows(string.Empty));
        Assert.False(entry.Allows("info"));
    }

    [Theory]
    [InlineData("/git")]
    [InlineData("git/")]
    public void Invalid_reason_rejects_leading_or_trailing_slash(string value) =>
        Assert.Equal($"路径 {value} 不能以 / 开头或结尾。", ToolPath.InvalidReason(value));

    [Fact]
    public void Invalid_reason_names_the_offending_segment()
    {
        Assert.Equal("路径 gi t 的段 gi t 不能为空或含空白与花括号。", ToolPath.InvalidReason("gi t"));
        Assert.Equal("路径 a{b} 的段 a{b} 不能为空或含空白与花括号。", ToolPath.InvalidReason("a{b}"));
        Assert.Contains("不能为空或含空白与花括号", ToolPath.InvalidReason("git//x"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("info/GetTask")]
    public void Invalid_reason_accepts_blank_or_well_formed_paths(string value) =>
        Assert.Null(ToolPath.InvalidReason(value));
}
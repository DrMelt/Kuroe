using Xunit;

namespace Kuroe.Tool.Tests;

/// <summary>工具路径：既是模型调用名，也是白名单匹配的原文，末段是函数名；格式在入口校验。</summary>
public sealed class ToolPathTests
{
    [Fact]
    public void Name_is_the_last_segment()
    {
        Assert.Equal(new ToolName("GetTime"), new ToolPath("GetTime").Name);
        Assert.Equal(new ToolName("ReadFile"), new ToolPath("file/read/ReadFile").Name);
        Assert.Equal(new ToolName(string.Empty), new ToolPath(string.Empty).Name);
    }

    [Theory]
    [InlineData("KuroeInfo", "KuroeInfo")]
    [InlineData("KuroeInfo", "KuroeInfo/GetTask")]
    [InlineData("KuroeInfo/GetTask", "KuroeInfo/GetTask")]
    [InlineData("file", "file/read/ReadFile")]
    public void Allows_accepts_the_entry_and_its_subtree(string entry, string path) =>
        Assert.True(new ToolPath(entry).Allows(path));

    [Theory]
    [InlineData("KuroeInfo", "KuroeInfoX")]
    [InlineData("KuroeInfo/GetTask", "KuroeInfo")]
    [InlineData("Kuroeinfo", "KuroeInfo")]
    public void Allows_rejects_paths_outside_the_entry(string entry, string path) =>
        Assert.False(new ToolPath(entry).Allows(path));

    [Fact]
    public void Empty_entry_allows_nothing()
    {
        ToolPath entry = new(string.Empty);

        Assert.False(entry.Allows(string.Empty));
        Assert.False(entry.Allows("KuroeInfo"));
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
    [InlineData("KuroeInfo/GetTask")]
    public void Invalid_reason_accepts_blank_or_well_formed_paths(string value) =>
        Assert.Null(ToolPath.InvalidReason(value));
}
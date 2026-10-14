using Kuroe.Flows.Shared.StaticGraph;
using Xunit;

namespace Kuroe.Flows.Shared.Tests;

/// <summary>能力路径：白名单按路径前缀放行，路径格式在入口校验。</summary>
public sealed class CapabilityPathTests
{
    [Theory]
    [InlineData("info", "info")]
    [InlineData("info", "info/GetTask")]
    [InlineData("info/GetTask", "info/GetTask")]
    public void Allows_accepts_the_entry_and_its_subtree(string entry, string path) =>
        Assert.True(new CapabilityPath(entry).Allows(path));

    [Theory]
    [InlineData("info", "infoX")]
    [InlineData("info/GetTask", "info")]
    [InlineData("Info", "info")]
    public void Allows_rejects_paths_outside_the_entry(string entry, string path) =>
        Assert.False(new CapabilityPath(entry).Allows(path));

    [Fact]
    public void Empty_entry_allows_nothing()
    {
        CapabilityPath entry = new(string.Empty);

        Assert.False(entry.Allows(string.Empty));
        Assert.False(entry.Allows("info"));
    }

    [Theory]
    [InlineData("/git")]
    [InlineData("git/")]
    public void Invalid_reason_rejects_leading_or_trailing_slash(string value) =>
        Assert.Equal($"路径 {value} 不能以 / 开头或结尾。", CapabilityPath.InvalidReason(value));

    [Fact]
    public void Invalid_reason_names_the_offending_segment()
    {
        Assert.Equal("路径 gi t 的段 gi t 不能为空或含空白与花括号。", CapabilityPath.InvalidReason("gi t"));
        Assert.Equal("路径 a{b} 的段 a{b} 不能为空或含空白与花括号。", CapabilityPath.InvalidReason("a{b}"));
        Assert.Contains("不能为空或含空白与花括号", CapabilityPath.InvalidReason("git//x"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("info/GetTask")]
    public void Invalid_reason_accepts_blank_or_well_formed_paths(string value) =>
        Assert.Null(CapabilityPath.InvalidReason(value));
}

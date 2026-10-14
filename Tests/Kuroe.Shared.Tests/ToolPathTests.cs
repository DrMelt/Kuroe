using Kuroe.Shared.Executions.Tools;
using Xunit;

namespace Kuroe.Shared.Tests;

/// <summary>工具路径的解析：空白、花括号与首尾斜杠在入口校验。</summary>
public sealed class ToolPathTests
{
    [Fact]
    public void Tool_path_segment_with_whitespace_is_rejected() =>
        Assert.Equal("路径 a b 的段 a b 不能为空或含空白与花括号。", ToolPath.InvalidReason("a b"));

    [Fact]
    public void Tool_path_with_trailing_slash_is_rejected() =>
        Assert.Equal("路径 git/ 不能以 / 开头或结尾。", ToolPath.InvalidReason("git/"));

    [Fact]
    public void Blank_tool_path_has_no_invalid_reason() =>
        Assert.Null(ToolPath.InvalidReason("  "));
}

using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;
using Xunit;

namespace Kuroe.Tests;

/// <summary>流程值对象的解析：直接调用各 Create，断言规范化结果或错误说明。</summary>
public sealed class FlowValueObjectTests
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

    [Theory]
    [InlineData("  ")]
    [InlineData("结论@甲")]
    public void Invalid_port_name_is_rejected(string name) =>
        Assert.Equal(
            "端口名不能为空或含 @。",
            PortName.Create(name).ErrorsOrEmptyList.Single().Description);

    [Fact]
    public void Flow_name_with_surrounding_spaces_is_trimmed() =>
        Assert.Equal(new FlowName("默认"), FlowName.Create("  默认 ").Value);

    [Fact]
    public void Blank_flow_name_is_rejected() =>
        Assert.Equal(
            "流程名不能为空。",
            FlowName.Create("  ").ErrorsOrEmptyList.Single().Description);
}

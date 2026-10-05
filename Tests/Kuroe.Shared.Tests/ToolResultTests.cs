using ErrorOr;
using Kuroe.Shared.Executions.Tools;
using Xunit;

namespace Kuroe.Shared.Tests;

/// <summary>工具调用结果的文本渲染：成功原样返回，错误按类型加前缀并逐条合并。</summary>
public sealed class ToolResultTests
{
    [Fact]
    public void Render_returns_value_when_succeeded() =>
        Assert.Equal("已写入 5 字符。", ToolResult.Render("已写入 5 字符。"));

    [Fact]
    public void Render_prefixes_validation_as_rejection() =>
        Assert.Equal("被拒绝：缺少参数 path。", ToolResult.Render(Error.Validation("Tool.Argument", "缺少参数 path。")));

    [Fact]
    public void Render_prefixes_not_found_as_rejection() =>
        Assert.Equal("被拒绝：没有名为 missing 的流程。", ToolResult.Render(Error.NotFound("Flow.NotFound", "没有名为 missing 的流程。")));

    [Fact]
    public void Render_prefixes_failure_as_failure() =>
        Assert.Equal("失败：读取 a.txt 失败：拒绝访问。", ToolResult.Render(Error.Failure("Tool.ReadFailed", "读取 a.txt 失败：拒绝访问。")));

    [Fact]
    public void Render_prefixes_unexpected_as_internal() =>
        Assert.Equal("内部错误：该 run 没有归属任务。", ToolResult.Render(Error.Unexpected("Tool.Internal", "该 run 没有归属任务。")));

    [Fact]
    public void Render_joins_errors_each_with_own_prefix()
    {
        List<Error> errors = [Error.Validation("Tool.Argument", "条目缺少字段。"), Error.Validation("Tool.Argument", "分支越界。")];

        Assert.Equal("被拒绝：条目缺少字段。；被拒绝：分支越界。", ToolResult.Render(errors));
    }
}

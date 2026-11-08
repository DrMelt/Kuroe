using ErrorOr;
using Xunit;

namespace Kuroe.Tool.Tests;

/// <summary>工具调用结果的文本渲染：成功原样返回，失败合并错误说明。</summary>
public sealed class ToolResultTests
{
    [Fact]
    public void Render_returns_value_when_succeeded() =>
        Assert.Equal("已写入 5 字符。", ToolResult.Render("已写入 5 字符。"));

    [Fact]
    public void Render_returns_validation_description() =>
        Assert.Equal("缺少参数 path。", ToolResult.Render(Error.Validation("Tool.Argument", "缺少参数 path。")));

    [Fact]
    public void Render_returns_not_found_description() =>
        Assert.Equal("没有名为 missing 的流程。", ToolResult.Render(Error.NotFound("Flow.NotFound", "没有名为 missing 的流程。")));

    [Fact]
    public void Render_returns_failure_description() =>
        Assert.Equal("读取 a.txt 失败：拒绝访问。", ToolResult.Render(Error.Failure("Tool.ReadFailed", "读取 a.txt 失败：拒绝访问。")));

    [Fact]
    public void Render_returns_unexpected_description() =>
        Assert.Equal("该执行没有归属任务。", ToolResult.Render(Error.Unexpected("Tool.Internal", "该执行没有归属任务。")));

    [Fact]
    public void Render_joins_error_descriptions()
    {
        List<Error> errors = [Error.Validation("Tool.Argument", "条目缺少字段。"), Error.Validation("Tool.Argument", "分支越界。")];

        Assert.Equal("条目缺少字段。；分支越界。", ToolResult.Render(errors));
    }
}

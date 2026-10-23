using ErrorOr;
using Kuroe.Flow.Shared.Execution;
using Xunit;

namespace Kuroe.Cli.Tests;

/// <summary>stdout 与 stderr 的分流与行闭合。</summary>
public sealed class CliOutputTests
{
    [Fact]
    public void Text_and_lines_go_to_stdout_and_errors_go_to_stderr()
    {
        StringWriter output = new();
        StringWriter error = new();
        CliOutput cli = new(output, error);

        cli.Text("回答");
        cli.EnsureLine();
        cli.Line("完成");
        cli.Hint("提示");
        cli.Report([Error.Failure("Test.Code", "失败说明")]);

        Assert.Equal($"回答{Environment.NewLine}完成{Environment.NewLine}", output.ToString());
        Assert.Equal($"提示{Environment.NewLine}错误：Test.Code：失败说明{Environment.NewLine}", error.ToString());
    }

    [Fact]
    public void Tool_call_writes_invocation_and_result_to_stderr()
    {
        StringWriter output = new();
        StringWriter error = new();
        CliOutput cli = new(output, error);

        cli.ToolCall(new ToolCallRecord("file/read", "{\"path\":\"a\"}", "内容", false));
        cli.ToolCall(new ToolCallRecord("file/read", "{}", "失败原因", true));

        Assert.Empty(output.ToString());
        Assert.Equal(
            $"工具 file/read {{\"path\":\"a\"}}{Environment.NewLine}结果 内容{Environment.NewLine}" +
            $"工具 file/read {{}}{Environment.NewLine}失败 失败原因{Environment.NewLine}",
            error.ToString());
    }
}
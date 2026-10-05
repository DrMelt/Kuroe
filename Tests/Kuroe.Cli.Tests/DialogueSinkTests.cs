using Kuroe.Cli.Views;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Tools;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Cli.Tests;

/// <summary>前台对话终端的工具调用行与失败行呈现。</summary>
public sealed class DialogueSinkTests
{
    [Fact]
    public void OnToolCall_success_prints_tool_result_line()
    {
        Terminal terminal = Ui.NewTerminal(out TestConsole output, out _);
        DialogueSink sink = new(terminal);

        sink.OnToolCall(new ToolCallRecord(new ToolName("WriteFile"), """{"path":"a.txt"}""", "已写入 5 字符。", false));

        Assert.Contains("工具 > WriteFile", output.Output);
        Assert.Contains("结果 > 已写入 5 字符。", output.Output);
        Assert.DoesNotContain("失败 >", output.Output);
    }

    [Fact]
    public void OnToolCall_failure_prints_warning_line()
    {
        Terminal terminal = Ui.NewTerminal(out TestConsole output, out _);
        DialogueSink sink = new(terminal);

        sink.OnToolCall(new ToolCallRecord(new ToolName("WriteFile"), """{"path":"a.txt"}""", "被拒绝：缺少参数。", true));

        Assert.Contains("失败 > 被拒绝：缺少参数。", output.Output);
        Assert.DoesNotContain("结果 >", output.Output);
    }
}

using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Turns;

namespace Kuroe.Cli.Views;

/// <summary>对话回合的终端呈现：文本增量直接写出，工具调用行由终端保证另起一行。</summary>
internal sealed class DialogueSink(Terminal terminal) : ITurnSink
{
    public void OnText(string delta) => terminal.Append(delta);

    public void OnToolCall(ToolCallRecord record)
    {
        terminal.ToolCall("工具 > ");
        terminal.ToolCall(record.Arguments.Length == 0
            ? record.Name
            : $"{record.Name} {record.Arguments}");

        if (record.Failed)
        {
            terminal.Warn("失败 > ");
            terminal.Warn(record.Outcome);
            return;
        }

        terminal.ToolResult("结果 > ");
        terminal.ToolResult(record.Outcome);
    }
}

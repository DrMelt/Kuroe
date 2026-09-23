using Kuroe.Agent;

namespace Kuroe.Cli.Views;

/// <summary>前台回合的终端呈现：文本增量直接写出，工具调用写成调用行与结果行。</summary>
internal sealed class DialogueSink(Terminal terminal) : ITurnSink
{
    public void OnText(string delta) => terminal.Append(delta);

    public void OnToolCall(ToolCallRecord record)
    {
        terminal.ToolCall($"工具 > {record.Name} {record.Arguments}");

        if (record.Failed)
        {
            terminal.Warn($"失败 > {record.Outcome}");
            return;
        }

        terminal.ToolResult($"结果 > {record.Outcome}");
    }
}

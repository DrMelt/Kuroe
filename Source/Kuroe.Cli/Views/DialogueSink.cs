using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;

namespace Kuroe.Cli.Views;

/// <summary>对话过程的终端呈现：文本增量直接写出，一次工具调用占两行。</summary>
internal sealed class DialogueSink(Terminal terminal) : IProcessSink
{
    public void OnText(string delta) => terminal.Append(delta);

    public void OnToolCall(ToolCallRecord record)
    {
        terminal.ToolCall(record.Arguments.Length == 0
            ? $"工具 {record.Name}"
            : $"工具 {record.Name} {record.Arguments}");

        if (record.Failed)
        {
            terminal.Warn($"失败 {record.Outcome}");
            return;
        }

        terminal.ToolResult($"结果 {record.Outcome}");
    }
}

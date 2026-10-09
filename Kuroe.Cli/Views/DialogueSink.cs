using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Turns;

namespace Kuroe.Cli.Views;

/// <summary>对话回合的终端呈现：文本增量直接写出，工具调用前先补齐换行以保证调用行独立。</summary>
internal sealed class DialogueSink(Terminal terminal) : ITurnSink
{
    private bool _lineOpen;

    public void OnText(string delta)
    {
        terminal.Append(delta);
        _lineOpen = !delta.EndsWith('\n');
    }

    public void OnToolCall(ToolCallRecord record)
    {
        if (_lineOpen)
        {
            terminal.NewLine();
            _lineOpen = false;
        }

        terminal.ToolCall("工具 > ");
        terminal.ToolCall(record.Arguments.Length == 0
            ? record.Name.Value
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

    /// <summary>清掉行状态，供新回合开始时调用。</summary>
    public void Reset() => _lineOpen = false;
}

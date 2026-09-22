using Kuroe.Agent;

namespace Kuroe.Cli;

/// <summary>工具调用记录的终端呈现：一次调用写成调用行与结果行。</summary>
internal sealed class ConsoleToolCalls(ToolCallLog log, Terminal terminal)
{
    private readonly ToolCallLog _log = log;
    private readonly Terminal _terminal = terminal;

    /// <summary>订阅记录，由对话循环在开始时调用一次。</summary>
    public void Attach() => _log.Recorded += Report;

    private void Report(ToolCallRecord record)
    {
        _terminal.ToolCall($"工具 > {record.Name} {record.Arguments}");

        if (record.Failed)
        {
            _terminal.Warn($"失败 > {record.Outcome}");
            return;
        }

        _terminal.ToolResult($"结果 > {record.Outcome}");
    }
}
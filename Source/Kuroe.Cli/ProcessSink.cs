using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;

namespace Kuroe.Cli;

/// <summary>执行过程的命令行呈现：文本增量写 stdout，工具调用写 stderr。</summary>
internal sealed class ProcessSink(CliOutput output) : IProcessSink
{
    public void OnText(string delta) => output.Text(delta);

    public void OnToolCall(ToolCallRecord record) => output.ToolCall(record);
}
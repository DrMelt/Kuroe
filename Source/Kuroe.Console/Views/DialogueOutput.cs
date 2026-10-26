using Kuroe.Flow.Shared.Execution.Process;

namespace Kuroe.Console.Views;

/// <summary>对话过程的终端呈现：文本增量直接写出，过程条目按文本行展开。</summary>
internal sealed class DialogueOutput(Terminal terminal)
{
    public void Text(string delta) => terminal.Append(delta);

    public void Entry(JournalEntry entry)
    {
        string[] lines = entry.Text.Split('\n');
        terminal.ToolCall($"{entry.Label} {lines[0]}");

        for (int index = 1; index < lines.Length; index++)
        {
            if (entry.Level != JournalLevel.Information)
            {
                terminal.Warn(lines[index]);
                continue;
            }

            terminal.ToolResult(lines[index]);
        }
    }
}

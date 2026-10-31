using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Spectre.Console;

namespace Kuroe.Console.Views;

/// <summary>对话过程的终端呈现：回复与思考按内容种类分段写出，过程条目按文本行展开。</summary>
internal sealed class DialogueOutput(Terminal terminal)
{
    private readonly Lock _gate = new();
    private ProcessContentKind? _open;
    private bool _lineOpen;

    /// <summary>开始新一轮对话前清掉上一轮的内容块与行状态。</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _open = null;
            _lineOpen = false;
        }
    }

    /// <summary>写出一段流式内容。内容种类切换时结束当前行，思考块以“思考”开头。</summary>
    public void Content(ProcessContent content)
    {
        if (content.Delta.Length == 0)
        {
            return;
        }

        lock (_gate)
        {
            bool starts = _open != content.Kind;
            if (starts)
            {
                CloseContent();
                _open = content.Kind;
            }

            string text = starts && content.Kind == ProcessContentKind.Reasoning
                ? $"思考 {content.Delta}"
                : content.Delta;
            terminal.Append(text, StyleOf(content.Kind));
            _lineOpen = !content.Delta.EndsWith('\n');
        }
    }

    public void Entry(JournalEntry entry)
    {
        lock (_gate)
        {
            CloseContent();
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

    private void CloseContent()
    {
        if (_open is null)
        {
            return;
        }

        if (_lineOpen)
        {
            terminal.NewLine();
        }

        _open = null;
        _lineOpen = false;
    }

    private static Style StyleOf(ProcessContentKind kind) =>
        kind == ProcessContentKind.Reasoning ? Styles.Reasoning : Style.Plain;
}

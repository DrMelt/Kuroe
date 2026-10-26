using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;

namespace Kuroe.Tests.Support;

/// <summary>测试用的过程记录：保存条目并按文本追加，用于断言执行期间写入的内容。
/// 文本增量与工具调用在写入后经事件播发。</summary>
internal sealed class RecordingJournal : IProcessJournal
{
    private readonly Lock _gate = new();
    private readonly List<JournalEntry> _entries = [];

    /// <summary>文本追加后给出该增量。</summary>
    public event Action<string>? TextAppended;

    /// <summary>工具调用记录后给出该记录。</summary>
    public event Action<ToolCallRecord>? ToolCallRecorded;

    /// <summary>追加一条过程记录，工具调用随后播发。</summary>
    public void Append(JournalEntry entry)
    {
        lock (_gate)
        {
            _entries.Add(entry);
        }

        if (entry is ToolCallEntry toolCall)
        {
            ToolCallRecorded?.Invoke(toolCall.Call);
        }
    }

    /// <summary>追加文本增量，与末尾的文本记录合并，随后播发该增量。</summary>
    public void AppendText(string delta)
    {
        if (delta.Length == 0)
        {
            return;
        }

        lock (_gate)
        {
            if (_entries.Count > 0 && _entries[^1] is TextEntry last)
            {
                _entries[^1] = last with { Text = last.Text + delta };
            }
            else
            {
                _entries.Add(new TextEntry(delta));
            }
        }

        TextAppended?.Invoke(delta);
    }

    /// <summary>已记录的条目，按发生顺序。</summary>
    public IReadOnlyList<JournalEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries];
            }
        }
    }

    /// <summary>测试记录不丢弃条目。</summary>
    public int DroppedEntries => 0;
}
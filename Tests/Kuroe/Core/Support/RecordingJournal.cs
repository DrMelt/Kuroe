using Kuroe.Flow.Shared.Execution.Process;

namespace Kuroe.Tests.Support;

/// <summary>测试用的过程记录：保存条目并按文本追加，用于断言执行期间写入的内容。</summary>
internal sealed class RecordingJournal : IProcessJournal
{
    private readonly Lock _gate = new();
    private readonly List<JournalEntry> _entries = [];

    /// <summary>追加一条过程记录。</summary>
    public void Append(JournalEntry entry)
    {
        lock (_gate)
        {
            _entries.Add(entry);
        }
    }

    /// <summary>追加文本增量，与末尾的文本记录合并。</summary>
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
                return;
            }

            _entries.Add(new TextEntry(delta));
        }
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
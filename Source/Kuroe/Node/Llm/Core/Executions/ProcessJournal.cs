using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;

namespace Kuroe.Node.Llm.Executions;

/// <summary>一次执行的过程记录：追加与读取共用一把锁，文本增量与末尾的文本条目合并。
/// 文本增量与工具调用在写入后经事件播发。</summary>
internal sealed class ProcessJournal : IProcessJournal
{
    /// <summary>单条文本记录的上限，超出后另起一条，避免逐字增量反复拷贝整段。</summary>
    private const int SegmentLimit = 4096;

    /// <summary>保留的记录条数，超出后丢最早的记录。</summary>
    private const int EntryLimit = 400;

    private readonly Lock _gate = new();
    private readonly List<JournalEntry> _entries = [];
    private int _dropped;

    /// <summary>文本追加后给出该增量。</summary>
    public event Action<string>? TextAppended;

    /// <summary>工具调用记录后给出该记录。</summary>
    public event Action<ToolCallRecord>? ToolCallRecorded;

    /// <summary>追加一条过程记录，工具调用随后播发。</summary>
    public void Append(JournalEntry entry)
    {
        lock (_gate)
        {
            AppendCore(entry);
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
            if (_entries.Count > 0 && _entries[^1] is TextEntry last && last.Text.Length + delta.Length <= SegmentLimit)
            {
                _entries[^1] = last with { Text = last.Text + delta };
            }
            else
            {
                AppendCore(new TextEntry(delta));
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

    /// <summary>因超出保留条数被丢弃的条数。</summary>
    public int DroppedEntries
    {
        get
        {
            lock (_gate)
            {
                return _dropped;
            }
        }
    }

    /// <summary>要求持有 <see cref="_gate"/>。</summary>
    private void AppendCore(JournalEntry entry)
    {
        _entries.Add(entry);
        if (_entries.Count > EntryLimit)
        {
            _entries.RemoveAt(0);
            _dropped++;
        }
    }
}
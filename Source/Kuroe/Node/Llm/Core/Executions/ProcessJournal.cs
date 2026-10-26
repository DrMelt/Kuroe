using Kuroe.Flow.Shared.Execution.Process;
using Kuroe.Tool;

namespace Kuroe.Node.Llm.Executions;

/// <summary>一次执行的过程记录：追加与读取共用一把锁，文本增量与末尾的文本条目合并。
/// 文本增量与需要即时呈现的条目写入后、进度变化后经事件播发。</summary>
internal sealed class ProcessJournal : IProcessJournal
{
    /// <summary>单条文本记录的上限，超出后另起一条，避免逐字增量反复拷贝整段。</summary>
    private const int SegmentLimit = 4096;

    /// <summary>保留的记录条数，超出后丢最早的记录。</summary>
    private const int EntryLimit = 400;

    private const string PromptLabel = "输入";
    private const string ReplyLabel = "回复";
    private const string ToolLabel = "工具";
    private const string DiscardedLabel = "丢弃";
    private const string DiscardedText = "本轮未计入上下文。";

    private readonly Lock _gate = new();
    private readonly List<JournalEntry> _entries = [];
    private int _dropped;

    /// <summary>文本追加后给出该增量。</summary>
    public event Action<string>? TextAppended;

    /// <summary>需要即时呈现的条目写入后给出该条目。</summary>
    public event Action<JournalEntry>? EntryAppended;

    /// <summary>执行进度变化后给出进度文本。</summary>
    public event Action<string>? ProgressReported;

    /// <summary>追加一条发给模型的输入。</summary>
    public void AppendPrompt(string input) =>
        Append(new JournalEntry(DateTimeOffset.UtcNow, JournalLevel.Information, PromptLabel, input));

    /// <summary>追加文本增量，与末尾的回复记录合并，随后播发该增量。</summary>
    public void AppendText(string delta)
    {
        if (delta.Length == 0)
        {
            return;
        }

        lock (_gate)
        {
            if (_entries.Count > 0
                && _entries[^1].Label == ReplyLabel
                && _entries[^1].Text.Length + delta.Length <= SegmentLimit)
            {
                JournalEntry last = _entries[^1];
                _entries[^1] = last with { Text = last.Text + delta };
            }
            else
            {
                AppendCore(new JournalEntry(DateTimeOffset.UtcNow, JournalLevel.Information, ReplyLabel, delta));
            }
        }

        TextAppended?.Invoke(delta);
    }

    /// <summary>追加一次工具调用，随后播发进度与条目。</summary>
    public void AppendToolCall(ToolCallRecord call)
    {
        string invocation = call.Arguments.Length == 0
            ? call.Name
            : $"{call.Name} {call.Arguments}";
        string outcome = call.Failed
            ? $"失败 {call.Outcome}"
            : $"结果 {call.Outcome}";
        JournalEntry entry = new(
            DateTimeOffset.UtcNow,
            call.Failed ? JournalLevel.Warning : JournalLevel.Information,
            ToolLabel,
            $"{invocation}\n{outcome}");

        Append(entry);
        ProgressReported?.Invoke($"调用工具 {call.Name}");
        EntryAppended?.Invoke(entry);
    }

    /// <summary>追加一条本轮未计入上下文的说明。</summary>
    public void AppendDiscarded() =>
        Append(new JournalEntry(DateTimeOffset.UtcNow, JournalLevel.Warning, DiscardedLabel, DiscardedText));

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

    /// <summary>追加一条过程记录。</summary>
    private void Append(JournalEntry entry)
    {
        lock (_gate)
        {
            AppendCore(entry);
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
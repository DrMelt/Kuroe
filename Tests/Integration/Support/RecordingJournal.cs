using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Integration.Tests.Support;

/// <summary>测试用的过程记录：保存条目并按文本追加，用于断言执行期间写入的内容。
/// 文本增量与需要即时呈现的条目写入后、进度变化后经事件播发。</summary>
internal sealed class RecordingJournal : IProcessJournal
{
    private const string ReplyLabel = "回复";
    private const string ToolLabel = "工具";

    private readonly Lock _gate = new();
    private readonly List<JournalEntry> _entries = [];

    /// <summary>文本追加后给出该增量。</summary>
    public event Action<string>? TextAppended;

    /// <summary>需要即时呈现的条目写入后给出该条目。</summary>
    public event Action<JournalEntry>? EntryAppended;

    /// <summary>执行进度变化后给出进度文本。</summary>
    public event Action<string>? ProgressReported;

    /// <summary>追加一条过程记录。</summary>
    public void Append(JournalEntry entry)
    {
        lock (_gate)
        {
            _entries.Add(entry);
        }
    }

    /// <summary>追加一条发给模型的输入。</summary>
    public void AppendPrompt(string input) =>
        Append(new JournalEntry(DateTimeOffset.UtcNow, JournalLevel.Information, "输入", input));

    /// <summary>追加一次工具调用，随后播发进度与条目。</summary>
    public void AppendToolCall(string name, string arguments, string outcome, bool failed)
    {
        string invocation = arguments.Length == 0 ? name : $"{name} {arguments}";
        string result = failed ? $"失败 {outcome}" : $"结果 {outcome}";
        JournalEntry entry = new(
            DateTimeOffset.UtcNow,
            failed ? JournalLevel.Warning : JournalLevel.Information,
            ToolLabel,
            $"{invocation}\n{result}");

        Append(entry);
        ProgressReported?.Invoke($"调用工具 {name}");
        EntryAppended?.Invoke(entry);
    }

    /// <summary>追加文本增量，与末尾的回复记录合并，随后播发该增量。</summary>
    public void AppendText(string delta)
    {
        if (delta.Length == 0)
        {
            return;
        }

        lock (_gate)
        {
            if (_entries.Count > 0 && _entries[^1].Label == ReplyLabel)
            {
                JournalEntry last = _entries[^1];
                _entries[^1] = last with { Text = last.Text + delta };
            }
            else
            {
                _entries.Add(new JournalEntry(DateTimeOffset.UtcNow, JournalLevel.Information, ReplyLabel, delta));
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
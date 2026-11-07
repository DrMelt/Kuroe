namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>一次执行的过程记录：内容写入共享存储，条目按时间保留，变更后向外播发。</summary>
public sealed class ExecutionJournal : IProcessJournal
{
    private readonly ProcessJournalStore _store = new();
    private readonly Lock _progressGate = new();
    private string _progress = string.Empty;

    /// <inheritdoc/>
    public event ProcessContentHandler? ContentAppended;

    /// <inheritdoc/>
    public event JournalEntryHandler? EntryAppended;

    /// <inheritdoc/>
    public event ProgressTextHandler? ProgressReported;

    /// <summary>追加一条记录并播发。</summary>
    public void Append(string label, string text, JournalLevel level = JournalLevel.Information)
    {
        JournalEntry entry = new(DateTimeOffset.UtcNow, level, label, text);
        _store.Append(entry);
        EntryAppended?.Invoke(entry);
    }

    /// <summary>追加一条记录并播发。</summary>
    public void Append(JournalEntry entry)
    {
        _store.Append(entry);
        EntryAppended?.Invoke(entry);
    }

    /// <summary>追加一段执行产出的内容并播发，条目保留在记录里，不触发即时条目通知。</summary>
    public void AppendContent(ProcessContentKind kind, string delta)
    {
        string label = kind switch
        {
            ProcessContentKind.Reply => "输出",
            ProcessContentKind.Reasoning => "过程",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "未知的过程内容种类。"),
        };
        if (!_store.AppendDelta(label, delta))
        {
            return;
        }

        ReportProgress(kind == ProcessContentKind.Reasoning ? "思考" : "输出");
        ContentAppended?.Invoke(new ProcessContent(kind, delta));
    }

    /// <summary>报告当前活动，文本未变化时不播发。</summary>
    public void ReportProgress(string text)
    {
        lock (_progressGate)
        {
            if (_progress == text)
            {
                return;
            }

            _progress = text;
        }

        ProgressReported?.Invoke(text);
    }

    /// <inheritdoc/>
    public IReadOnlyList<JournalEntry> Entries => _store.Entries;
}
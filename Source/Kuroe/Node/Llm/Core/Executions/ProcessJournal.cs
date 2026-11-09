using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Tool;

namespace Kuroe.Node.Llm.Executions;

/// <summary>一次执行的过程记录：条目由共享存储保留且不按条数丢弃，同标签文本增量与末尾同类条目合并。
/// 流式内容、需要即时呈现的条目与进度变化经事件播发。</summary>
internal sealed class ProcessJournal : IProcessJournal
{
    /// <summary>同标签流式内容的合并上限，超出后另起一条，避免逐字增量反复拷贝整段。</summary>
    private const int SegmentLimit = 4096;

    private const string PromptLabel = "输入";
    private const string ReplyLabel = "回复";
    private const string ReasoningLabel = "思考";
    private const string ToolLabel = "工具";
    private const string DiscardedLabel = "丢弃";
    private const string DiscardedText = "本轮未计入上下文。";
    private const string RestoreLabel = "恢复";

    private readonly ProcessJournalStore _store = new(segmentLimit: SegmentLimit);
    private readonly Lock _progressGate = new();
    private string _progress = string.Empty;

    /// <summary>流式内容追加后给出该增量与种类。</summary>
    public event ProcessContentHandler? ContentAppended;

    /// <summary>需要即时呈现的条目写入后给出该条目。</summary>
    public event JournalEntryHandler? EntryAppended;

    /// <summary>执行进度变化后给出进度文本。</summary>
    public event ProgressTextHandler? ProgressReported;

    /// <summary>追加一条发给模型的输入，随后清空当前活动。</summary>
    public void AppendPrompt(string input)
    {
        _store.Append(new JournalEntry(DateTimeOffset.UtcNow, JournalLevel.Information, PromptLabel, input));
        ReportProgress(string.Empty);
    }

    /// <summary>追加一段流式内容，与末尾同类记录合并，随后播发该增量。</summary>
    public void AppendContent(ProcessContentKind kind, string delta)
    {
        if (_store.AppendDelta(LabelOf(kind), delta))
        {
            ReportProgress(ProgressOf(kind));
            ContentAppended?.Invoke(new ProcessContent(kind, delta));
        }
    }

    /// <summary>追加一次工具调用，随后播发条目。</summary>
    public void AppendToolCall(ToolCallRecord call)
    {
        string invocation = call.Arguments.Length == 0
            ? call.Name
            : $"{call.Name} {call.Arguments}";
        JournalEntry entry = new(
            DateTimeOffset.UtcNow,
            call.Failed ? JournalLevel.Warning : JournalLevel.Information,
            ToolLabel,
            $"{invocation}\n{call.Outcome}");

        _store.Append(entry);
        EntryAppended?.Invoke(entry);
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

    /// <summary>追加一条本轮未计入上下文的说明。</summary>
    public void AppendDiscarded() =>
        _store.Append(new JournalEntry(DateTimeOffset.UtcNow, JournalLevel.Warning, DiscardedLabel, DiscardedText));

    /// <summary>追加一条从恢复数据继续的说明。</summary>
    public void AppendRestored()
    {
        JournalEntry entry = new(
            DateTimeOffset.UtcNow,
            JournalLevel.Information,
            RestoreLabel,
            "从上次中断处恢复。");

        _store.Append(entry);
        EntryAppended?.Invoke(entry);
    }

    /// <summary>已记录的条目，按发生顺序。</summary>
    public IReadOnlyList<JournalEntry> Entries => _store.Entries;

    /// <summary>内容种类对应的记录标签。</summary>
    private static string LabelOf(ProcessContentKind kind) => kind switch
    {
        ProcessContentKind.Reply => ReplyLabel,
        ProcessContentKind.Reasoning => ReasoningLabel,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>内容种类对应的当前活动文本。</summary>
    private static string ProgressOf(ProcessContentKind kind) => kind switch
    {
        ProcessContentKind.Reply => "输出",
        ProcessContentKind.Reasoning => "思考",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.TestSupport;

namespace Kuroe.Integration.Tests.Support;

/// <summary>测试用的过程记录：保存条目并按内容种类追加，用于断言执行期间写入的内容。
/// 产出内容与需要即时呈现的条目写入后、进度变化后经事件播发。</summary>
internal sealed class RecordingJournal : IProcessJournal
{
    private const string ReplyLabel = "回复";
    private const string ReasoningLabel = "思考";
    private const string ToolLabel = "工具";

    private readonly Lock _gate = new();
    private readonly List<JournalEntry> _entries = [];
    private string _progress = string.Empty;

    /// <summary>产出内容追加后给出该内容与种类。</summary>
    public event ProcessContentHandler? ContentAppended;

    /// <summary>需要即时呈现的条目写入后给出该条目。</summary>
    public event JournalEntryHandler? EntryAppended;

    /// <summary>执行进度变化后给出进度文本。</summary>
    public event ProgressTextHandler? ProgressReported;

    /// <summary>追加一条过程记录。</summary>
    public void Append(JournalEntry entry)
    {
        lock (_gate)
        {
            _entries.Add(entry);
        }
    }

    /// <summary>追加一条发给模型的输入，随后清空当前活动。</summary>
    public void AppendPrompt(string input)
    {
        Append(new JournalEntry(TestTime.At, JournalLevel.Information, "输入", input));
        ReportProgress(string.Empty);
    }

    /// <summary>追加一次工具调用，随后播发条目。</summary>
    public void AppendToolCall(string name, string arguments, string outcome, bool failed)
    {
        string invocation = arguments.Length == 0 ? name : $"{name} {arguments}";
        JournalEntry entry = new(
            TestTime.At,
            failed ? JournalLevel.Warning : JournalLevel.Information,
            ToolLabel,
            $"{invocation}\n{outcome}");

        Append(entry);
        EntryAppended?.Invoke(entry);
    }

    /// <summary>报告当前活动，文本未变化时不播发。</summary>
    public void ReportProgress(string text)
    {
        lock (_gate)
        {
            if (_progress == text)
            {
                return;
            }

            _progress = text;
        }

        ProgressReported?.Invoke(text);
    }

    /// <summary>追加一段产出内容，与末尾同类记录合并，随后播发该内容。</summary>
    public void AppendContent(ProcessContentKind kind, string delta)
    {
        if (delta.Length == 0)
        {
            return;
        }

        (string label, string progress) = kind switch
        {
            ProcessContentKind.Reply => (ReplyLabel, "输出"),
            ProcessContentKind.Reasoning => (ReasoningLabel, "思考"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        lock (_gate)
        {
            if (_entries.Count > 0 && _entries[^1].Label == label)
            {
                JournalEntry last = _entries[^1];
                _entries[^1] = last with { Text = last.Text + delta };
            }
            else
            {
                _entries.Add(new JournalEntry(TestTime.At, JournalLevel.Information, label, delta));
            }
        }

        ReportProgress(progress);
        ContentAppended?.Invoke(new ProcessContent(kind, delta));
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

}
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Xunit;

namespace Kuroe.Flow.Tests;

/// <summary>执行的过程记录与过程事件：附加读取视图、播发、进度与收口后停止。</summary>
public sealed class ExecutionProcessTests
{
    [Fact]
    public void Snapshot_without_journal_has_no_entries()
    {
        Execution execution = new(new ExecutionId(1), Input());

        ExecutionSnapshot snapshot = execution.Snapshot();

        Assert.Equal(ExecutionState.Queued, snapshot.State);
        Assert.Empty(snapshot.Journal);
        Assert.Equal(0, snapshot.DroppedEntries);
    }

    [Fact]
    public void Attached_journal_appears_in_snapshot()
    {
        Execution execution = new(new ExecutionId(1), Input());
        InfoEntry entry = new("记录");
        TestJournal journal = new([entry], 3);

        execution.AttachJournal(journal);
        ExecutionSnapshot snapshot = execution.Snapshot();

        Assert.Same(entry, Assert.Single(snapshot.Journal));
        Assert.Equal(3, snapshot.DroppedEntries);
    }

    [Fact]
    public void Journal_text_and_tool_call_reach_subscribers_and_update_progress()
    {
        Execution execution = new(new ExecutionId(1), Input());
        TestJournal journal = new([]);
        List<string> texts = [];
        List<ToolCallRecord> calls = [];
        execution.AttachJournal(journal);
        execution.TextEmitted += texts.Add;
        execution.ToolCallRecorded += calls.Add;
        execution.MarkRunning();

        journal.EmitText("增量");
        ToolCallRecord call = new("GetLocalTime", "{}", "2026-10-18 19:10:48", false);
        journal.EmitToolCall(call);

        Assert.Equal(["增量"], texts);
        Assert.Equal([call], calls);
        Assert.Equal("调用工具 GetLocalTime", execution.Snapshot().Progress);
    }

    [Fact]
    public void Settled_execution_stops_events_and_keeps_journal()
    {
        Execution execution = new(new ExecutionId(1), Input());
        TestJournal journal = new([new TextEntry("保留")]);
        execution.AttachJournal(journal);
        int texts = 0;
        int calls = 0;
        execution.TextEmitted += _ => texts++;
        execution.ToolCallRecorded += _ => calls++;
        execution.MarkRunning();
        execution.MarkSucceeded();

        journal.EmitText("后");
        journal.EmitToolCall(new ToolCallRecord("GetLocalTime", "{}", "完成", false));

        Assert.Equal(0, texts);
        Assert.Equal(0, calls);
        Assert.Equal(string.Empty, execution.Snapshot().Progress);
        Assert.Single(execution.Snapshot().Journal);
    }

    [Fact]
    public void Every_subscriber_receives_text_and_tool_calls()
    {
        Execution execution = new(new ExecutionId(1), Input());
        TestJournal journal = new([]);
        List<string> firstTexts = [];
        List<string> secondTexts = [];
        List<ToolCallRecord> firstCalls = [];
        List<ToolCallRecord> secondCalls = [];
        execution.AttachJournal(journal);
        execution.TextEmitted += firstTexts.Add;
        execution.TextEmitted += secondTexts.Add;
        execution.ToolCallRecorded += firstCalls.Add;
        execution.ToolCallRecorded += secondCalls.Add;
        execution.MarkRunning();

        journal.EmitText("增量");
        ToolCallRecord call = new("GetLocalTime", "{}", "2026-10-18 19:10:48", false);
        journal.EmitToolCall(call);

        Assert.Equal(["增量"], firstTexts);
        Assert.Equal(["增量"], secondTexts);
        Assert.Equal([call], firstCalls);
        Assert.Equal([call], secondCalls);
    }

    [Fact]
    public void Subscription_after_settle_receives_nothing()
    {
        Execution execution = new(new ExecutionId(1), Input());
        TestJournal journal = new([]);
        execution.AttachJournal(journal);
        execution.MarkRunning();
        execution.MarkSucceeded();
        List<string> texts = [];
        List<ToolCallRecord> calls = [];
        execution.TextEmitted += texts.Add;
        execution.ToolCallRecorded += calls.Add;

        journal.EmitText("后");
        journal.EmitToolCall(new ToolCallRecord("GetLocalTime", "{}", "完成", false));

        Assert.Empty(texts);
        Assert.Empty(calls);
    }

    private static ExecutionInput Input() => new()
    {
        Task = new TaskId(1),
        Node = NodePath.Of("实施"),
        Type = TestNodeTypes.Executable,
    };

    private sealed class TestJournal(IReadOnlyList<JournalEntry> entries, int dropped = 0) : IProcessJournal
    {
        public IReadOnlyList<JournalEntry> Entries { get; } = entries;

        public int DroppedEntries { get; } = dropped;

        public event Action<string>? TextAppended;

        public event Action<ToolCallRecord>? ToolCallRecorded;

        public void EmitText(string delta) => TextAppended?.Invoke(delta);

        public void EmitToolCall(ToolCallRecord call) => ToolCallRecorded?.Invoke(call);
    }
}
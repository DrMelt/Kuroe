using ErrorOr;
using Xunit;
using NeutralExecution = Kuroe.Execution.ExecutionInstance;
using Kuroe.Execution.Shared.Contract;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Journal;
using Kuroe.Execution.Shared.Ports;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Restore;
using Kuroe.Execution.Shared.Runtime;
using Kuroe.Execution.Shared.State;
using Kuroe.Execution.Shared.Usage;

namespace Kuroe.Execution.Tests;

/// <summary>执行单元的状态机：启动、契约产出、中断与存档恢复。</summary>
public sealed class ExecutionTests
{
    [Fact]
    public void Mark_running_notifies_once_and_records_start()
    {
        NeutralExecution unit = new(new ExecutionId(1), Request());
        int started = 0;
        unit.Started += () => started++;

        unit.MarkRunning();
        unit.MarkRunning();

        Assert.Equal(ExecutionState.Running, unit.State);
        Assert.Equal(1, started);
        Assert.NotNull(unit.Snapshot().StartedAt);
    }

    [Fact]
    public void Submitted_outputs_survive_success()
    {
        NeutralExecution unit = new(new ExecutionId(1), Request());
        ExecutionPortName port = new("结论");
        unit.SubmitOutputs(new Dictionary<ExecutionPortName, ExecutionOutputValue>
        {
            [port] = new(port, "结果", false),
        });

        unit.MarkSucceeded(ExecutionOutput.FromPorts([new ExecutionOutputValue(port, "结果", false)]));

        ExecutionSnapshot snapshot = unit.Snapshot();
        Assert.Equal(ExecutionState.Succeeded, snapshot.State);
        Assert.Equal("结果", snapshot.Contract.Outputs![port].Text);
        Assert.Equal(ExecutionOutputKind.Ports, snapshot.Output?.Kind);
    }

    [Fact]
    public void Interrupt_then_settle_records_reason()
    {
        NeutralExecution unit = new(new ExecutionId(1), Request());
        unit.MarkRunning();

        unit.Interrupt("任务终止。");
        unit.MarkInterrupted();

        ExecutionSnapshot snapshot = unit.Snapshot();
        Assert.Equal(ExecutionState.Interrupted, snapshot.State);
        Assert.Contains("任务终止。", snapshot.Failures);
    }

    [Fact]
    public void Restoring_a_live_state_marks_interrupted()
    {
        NeutralExecution unit = NeutralExecution.Restore(
            new ExecutionId(1),
            Request(),
            DateTimeOffset.UnixEpoch,
            ExecutionState.Running,
            "执行中",
            DateTimeOffset.UnixEpoch,
            null,
            [],
            ExecutionUsage.None,
            null,
            ExecutionContract.Empty,
            null);

        Assert.Equal(ExecutionState.Interrupted, unit.State);
        Assert.Contains("进程退出前未收口。", unit.Snapshot().Failures);
    }

    [Fact]
    public async Task Run_rejects_a_target_that_does_not_match_the_request()
    {
        Kuroe.Execution.ExecutionRuntime runtime = new(new Kuroe.Execution.ExecutionDispatcher());
        IExecution execution = runtime.Create(new ExecutionId(1), Request());
        ExecutionTarget target = new(
            new ExecutionTargetId("其他"),
            new ExecutionRunnerKey("test"),
            EmptyExecutionParameters.Instance);

        ErrorOr<Success> result = await runtime.RunAsync(
            execution,
            new ExecutionInvocation(target, new UnusedFactory()));

        Assert.True(result.IsError);
        Assert.Equal(ExecutionState.Failed, execution.State);
    }

    private static ExecutionRequest Request() =>
        new(
            new ExecutionOwnerId("任务"),
            new ExecutionTargetId("节点"),
            [],
            [],
            new Dictionary<ExecutionPortName, string>(),
            []);

    [Fact]
    public void Journal_entries_content_and_progress_reach_the_snapshot()
    {
        NeutralExecution unit = new(new ExecutionId(1), Request());
        TestJournal journal = new();
        List<ProcessContent> contents = [];
        List<JournalEntry> entries = [];
        unit.ContentEmitted += contents.Add;
        unit.EntryEmitted += entries.Add;
        unit.AttachJournal(journal);
        unit.MarkRunning();

        journal.Content(ProcessContentKind.Reply, "增量");
        JournalEntry entry = new(DateTimeOffset.UnixEpoch, JournalLevel.Information, "工具", "记录");
        journal.Entry(entry);
        journal.Progress("调用工具");

        ExecutionSnapshot snapshot = unit.Snapshot();
        Assert.Equal([new ProcessContent(ProcessContentKind.Reply, "增量")], contents);
        Assert.Equal([entry], entries);
        Assert.Equal("调用工具", snapshot.Progress);
        Assert.Single(snapshot.Journal);
    }

    [Fact]
    public void Usage_reader_is_followed_until_settled()
    {
        NeutralExecution unit = new(new ExecutionId(1), Request());
        TestUsageReader reader = new()
        {
            Usage = new ExecutionUsage(1, 0, new TokenUsage(10, 2, 12, null, null)),
        };

        unit.AttachUsageReader(reader);

        Assert.Equal(reader.Usage, unit.Snapshot().Usage);

        ExecutionUsage settled = new(2, 0, new TokenUsage(30, 4, 34, null, null));
        unit.SettleUsage(settled);
        reader.Usage = new ExecutionUsage(3, 0, new TokenUsage(50, 6, 56, null, null));

        Assert.Equal(settled, unit.Snapshot().Usage);
    }

    [Fact]
    public void Restore_data_reader_is_kept_after_success()
    {
        NeutralExecution unit = new(new ExecutionId(1), Request());
        TestRestoreReader reader = new()
        {
            RestoreData = new ExecutionRestoreState("test", "data"),
        };

        unit.AttachRestoreDataReader(reader);
        unit.MarkRunning();
        unit.MarkSucceeded(ExecutionOutput.FromPorts([]));
        unit.SettleRestoreData(reader);

        Assert.Equal(new ExecutionRestoreState("test", "data"), unit.Snapshot().RestoreData);
    }

    [Fact]
    public void Started_event_fires_once()
    {
        NeutralExecution unit = new(new ExecutionId(1), Request());
        int started = 0;
        unit.Started += () => started++;

        unit.MarkRunning();
        unit.MarkRunning();

        Assert.Equal(1, started);
    }

    private sealed class UnusedFactory : IExecutionRunnerFactory
    {
        public ExecutionRunnerKey Key => new("test");

        public IExecutionRunner Create(IExecutionParameters parameters, ExecutionId execution) =>
            throw new InvalidOperationException("目标不一致时不应建立运行器。");
    }

    private sealed class TestJournal : IProcessJournal
    {
        private readonly List<JournalEntry> _entries = [];

        public IReadOnlyList<JournalEntry> Entries => _entries;

        public event ProcessContentHandler? ContentAppended;

        public event JournalEntryHandler? EntryAppended;

        public event ProgressTextHandler? ProgressReported;

        public void Content(ProcessContentKind kind, string delta) =>
            ContentAppended?.Invoke(new ProcessContent(kind, delta));

        public void Entry(JournalEntry entry)
        {
            _entries.Add(entry);
            EntryAppended?.Invoke(entry);
        }

        public void Progress(string text) => ProgressReported?.Invoke(text);
    }

    private sealed class TestUsageReader : IExecutionUsageSource
    {
        public ExecutionUsage Usage { get; set; } = ExecutionUsage.None;
    }

    private sealed class TestRestoreReader : IExecutionRestoreDataSource
    {
        public ExecutionRestoreState? RestoreData { get; set; }

        public event Action? RestoreDataChanged
        {
            add => _ = value;
            remove => _ = value;
        }
    }
}

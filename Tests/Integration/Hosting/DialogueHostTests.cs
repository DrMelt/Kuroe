using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Flow.Control;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.RuntimeGraph.Execution;
using Kuroe.Flow.Node.RuntimeGraph.Tasks;
using Kuroe.Node.Llm;
using Kuroe.Pending;
using Xunit;

namespace Kuroe.Integration.Tests.Hosting;

/// <summary>常驻对话任务的管理层宿主：无模型时报模型未选择、一轮对话落到代码流程、多轮在同一任务上接续、语言变化后重置才重建、模型变化后下一轮重建。</summary>
public sealed class DialogueHostTests
{
    [Fact]
    public async Task Without_model_reports_the_model_error()
    {
        using TestEnvironment environment = TestEnvironment.Create(seedCatalog: false);

        ErrorOr<Success> reply = await environment.Dialogue.ReplyAsync("你好", null);

        Assert.True(reply.IsError);
        Assert.Equal("当前未选择模型。", ErrorText(reply));
    }

    [Fact]
    public async Task With_model_replies_through_dialogue_task()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复你好";
        RecordingOutput observer = new();

        ErrorOr<Success> reply = await environment.Dialogue.ReplyAsync("你好", new ProcessObserver(observer.Content, observer.Entry));

        Assert.False(reply.IsError, ErrorText(reply));
        Assert.Equal(["回复你好"], observer.Texts);
    }

    [Fact]
    public async Task Rounds_continue_on_the_same_dialogue_task()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        int round = 0;
        environment.Runner.CodeReply = _ => $"回复{++round}";
        RecordingOutput observer = new();

        ErrorOr<Success> first = await environment.Dialogue.ReplyAsync("第一问", new ProcessObserver(observer.Content, observer.Entry));
        ErrorOr<Success> second = await environment.Dialogue.ReplyAsync("第二问", new ProcessObserver(observer.Content, observer.Entry));

        Assert.False(first.IsError, ErrorText(first));
        Assert.False(second.IsError, ErrorText(second));
        Assert.Equal(["回复1", "回复2"], observer.Texts);
    }

    [Fact]
    public async Task Language_change_waits_for_reset_before_rebuilding()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复";

        await environment.Dialogue.ReplyAsync("第一问", null);
        string firstOwner = environment.Runner.StartedCode[0].Request.Owner.Value;
        environment.Settings.Set("Assistant:Language", "English").ThrowIfError();
        await environment.Dialogue.ReplyAsync("第二问", null);
        environment.Dialogue.Reset();
        await environment.Dialogue.ReplyAsync("第三问", null);

        Assert.Equal(firstOwner, environment.Runner.StartedCode[1].Request.Owner.Value);
        Assert.NotEqual(firstOwner, environment.Runner.StartedCode[2].Request.Owner.Value);
    }

    /// <summary>模型变化后下一轮重建对话任务，新任务用新模型。</summary>
    [Fact]
    public async Task Model_change_rebuilds_the_dialogue_task_with_the_new_model()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复";

        await environment.Dialogue.ReplyAsync("第一问", null);
        environment.Catalog.AddModel(
            ModelName.Create("second").Value,
            ProviderName.Create("test").Value,
            ProviderModelName.Create("second").Value).ThrowIfError();
        environment.Models.Select(ModelName.Create("second").Value).ThrowIfError();
        await environment.Dialogue.ReplyAsync("第二问", null);

        Assert.Equal(
            ["fake", "second"],
            environment.Runner.StartedTargets
                .Select(target => Assert.IsType<LlmExecutionParameters>(target.Parameters).Model));
    }

    [Fact]
    public async Task Dialogue_round_emits_no_task_notice()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复你好";
        List<ExecutionNotice> notices = [];
        environment.Registry.Notified += notices.Add;

        ErrorOr<Success> reply = await environment.Dialogue.ReplyAsync("你好", null);

        Assert.False(reply.IsError, ErrorText(reply));
        Assert.Empty(notices);
    }

    [Fact]
    public async Task Consecutive_rounds_never_report_settlement()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        int round = 0;
        environment.Runner.CodeReply = _ => $"回复{++round}";
        RecordingOutput observer = new();

        for (int index = 1; index <= 8; index++)
        {
            ErrorOr<Success> reply = await environment.Dialogue.ReplyAsync($"第{index}问", new ProcessObserver(observer.Content, observer.Entry));

            Assert.False(reply.IsError, ErrorText(reply));
        }

        Assert.Equal(
            [.. Enumerable.Range(1, 8).Select(index => $"回复{index}")],
            observer.Texts);
        Assert.Single(environment.Runner.StartedCode.Select(execution => execution.Request.Owner).Distinct());
    }

    [Fact]
    public async Task A_round_hands_tool_calls_to_its_observer()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复你好";
        environment.Runner.CodeEmits = (_, journal) =>
            journal.AppendToolCall("GetLocalTime", "{}", "2026-10-18 19:10:48", false);
        RecordingOutput observer = new();

        ErrorOr<Success> reply = await environment.Dialogue.ReplyAsync("你好", new ProcessObserver(observer.Content, observer.Entry));

        Assert.False(reply.IsError, ErrorText(reply));
        JournalEntry entry = Assert.Single(observer.Entries);
        Assert.Equal("工具", entry.Label);
        Assert.Equal("GetLocalTime {}\n2026-10-18 19:10:48", entry.Text);
    }

    [Fact]
    public async Task A_round_records_node_entries_and_streams_content()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复你好";
        environment.Runner.CodeEmits = (_, journal) =>
        {
            journal.AppendPrompt("问题");
            journal.AppendContent(ProcessContentKind.Reply, "增量");
        };
        RecordingOutput observer = new();

        ErrorOr<Success> reply = await environment.Dialogue.ReplyAsync("你好", new ProcessObserver(observer.Content, observer.Entry));

        Assert.False(reply.IsError, ErrorText(reply));
        Assert.Equal(["增量", "回复你好"], observer.Texts);

        IExecution round = Assert.Single(environment.Runner.StartedCode);
        List<JournalEntry> entries = [.. round.Snapshot().Journal];
        Assert.Contains(entries, entry => entry.Label == "输入" && entry.Text == "问题");
        Assert.Contains(entries, entry => entry.Text.Contains("增量", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_round_hands_reasoning_to_its_observer()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => string.Empty;
        environment.Runner.CodeEmits = (_, journal) =>
            journal.AppendContent(ProcessContentKind.Reasoning, "先想");
        RecordingOutput observer = new();

        ErrorOr<Success> reply = await environment.Dialogue.ReplyAsync("你好", new ProcessObserver(observer.Content, observer.Entry));

        Assert.False(reply.IsError, ErrorText(reply));
        Assert.Equal(["先想"], observer.Reasonings);
        Assert.Empty(observer.Texts);
    }

    [Fact]
    public void Settled_dialogue_error_names_the_state_in_its_display_form()
    {
        Error error = PendingErrors.DialogueSettled(TaskState.Blocked);

        Assert.Equal("对话已阻塞，无法继续对话。", error.Description);
    }

    [Fact]
    public async Task Pause_interrupts_a_running_dialogue_round_and_resume_continues()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复";
        environment.Runner.Gate.Hold(NodePath.Of("回话"));

        Task<ErrorOr<Success>> running = environment.Dialogue.ReplyAsync("第一问", null);
        await environment.Runner.Gate.Entered(NodePath.Of("回话"));
        IExecution execution = Assert.Single(environment.Runner.StartedCode);
        ITaskFlowContext owner = environment.Executions.FindOwned(execution.Id).ThrowIfError().Owner;

        lock (owner.Gate)
        {
            owner.Pause().ThrowIfError();
            environment.Driver.Pause(owner);
        }

        await WaitUntilAsync(() => !execution.IsLive);
        Assert.Equal(Kuroe.Flow.Execution.ExecutionState.Interrupted, execution.State);
        Assert.True((await running.WaitAsync(TestContext.Current.CancellationToken)).IsError);

        lock (owner.Gate)
        {
            owner.Resume().ThrowIfError();
            environment.Driver.ResumeTask(owner);
        }

        environment.Runner.Gate.ClearHolds();

        ErrorOr<Success> reply = await environment.Dialogue.ReplyAsync("第二问", null);

        Assert.False(reply.IsError, ErrorText(reply));
        Assert.Equal(2, environment.Runner.StartedCode.Count);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.Fail("等待超时。");
    }

    private static string ErrorText(ErrorOr<Success> result) =>
        result.IsError ? string.Join("；", result.ErrorsOrEmptyList.Select(error => error.Description)) : string.Empty;

    /// <summary>记下收到的过程，供断言。</summary>
    private sealed class RecordingOutput
    {
        public List<string> Texts { get; } = [];

        public List<string> Reasonings { get; } = [];

        public List<JournalEntry> Entries { get; } = [];

        public void Content(ProcessContent content)
        {
            if (content.Kind == ProcessContentKind.Reasoning)
            {
                Reasonings.Add(content.Delta);
                return;
            }

            Texts.Add(content.Delta);
        }

        public void Entry(JournalEntry entry) => Entries.Add(entry);
    }
}
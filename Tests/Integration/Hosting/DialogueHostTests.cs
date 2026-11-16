using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Assistant.Dialogue;
using Kuroe.Configuration.Files;
using Kuroe.Control.Shared;
using Kuroe.Execution.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Tasks.Shared;
using Kuroe.Tool;
using Kuroe.Node.Llm;
using Kuroe.Runner;
using Kuroe.Pending;
using Xunit;

namespace Kuroe.Integration.Tests.Hosting;

/// <summary>内置助手会话宿主：无模型时报模型未选择、一轮落到代码流程、多轮在同一会话上接续、语言与模型只影响新建会话、会话隔离与忙时拒绝、历史与任务域生命周期。</summary>
public sealed class DialogueHostTests
{
    [Fact]
    public async Task Without_model_reports_the_model_error()
    {
        using TestEnvironment environment = TestEnvironment.Create(seedCatalog: false);

        ErrorOr<Success> reply = await environment.SayAsync("你好", null);

        Assert.True(reply.IsError);
        Assert.Equal("当前未选择模型。", ErrorText(reply));
    }

    [Fact]
    public async Task With_model_replies_through_dialogue_task()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复你好";
        RecordingOutput observer = new();

        ErrorOr<Success> reply = await environment.SayAsync("你好", new ProcessObserver(observer.Content, observer.Entry));

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

        ErrorOr<Success> first = await environment.SayAsync("第一问", new ProcessObserver(observer.Content, observer.Entry));
        ErrorOr<Success> second = await environment.SayAsync("第二问", new ProcessObserver(observer.Content, observer.Entry));

        Assert.False(first.IsError, ErrorText(first));
        Assert.False(second.IsError, ErrorText(second));
        Assert.Equal(["回复1", "回复2"], observer.Texts);
    }

    [Fact]
    public async Task Switching_mode_keeps_the_dialogue_task_and_changes_the_next_round_tools()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复";

        await environment.SayAsync("第一问", null);
        environment.Dialogue.SelectMode(environment.Session.Id, DialogueMode.Edit).ThrowIfError();
        await environment.SayAsync("第二问", null);

        Assert.Equal(DialogueMode.Edit, environment.Session.Mode);
        Assert.Equal(
            environment.Runner.StartedCode[0].Request.Owner.Value,
            environment.Runner.StartedCode[1].Request.Owner.Value);

        LlmParameters read = Parameters(environment, 0);
        LlmParameters edit = Parameters(environment, 1);

        Assert.DoesNotContain(read.Tools, tool => tool.Value == "file/write");
        Assert.Contains(edit.Tools, tool => tool.Value == "file/write");
        Assert.DoesNotContain(edit.Tools, tool => tool.Value == "git/write");
        Assert.DoesNotContain(edit.Tools, tool => tool.Value == "dotnet/write");
        Assert.Contains("只读模式", read.SystemPrompt[1], StringComparison.Ordinal);
        Assert.Contains("更改模式", edit.SystemPrompt[1], StringComparison.Ordinal);
    }

    /// <summary>建立会话时给出的模式作用于首轮。</summary>
    [Fact]
    public async Task The_mode_given_at_creation_applies_to_the_first_round()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复";
        environment.Dialogue.Create(mode: DialogueMode.Edit).ThrowIfError();

        await environment.SayAsync("你好", null);

        LlmParameters parameters = Parameters(environment, 0);

        Assert.Contains(parameters.Tools, tool => tool.Value == "file/write");
        Assert.Contains("更改模式", parameters.SystemPrompt[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rounds_continue_from_the_previous_execution_session_snapshot()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复";
        ExecutionRestoreState seed = new("fake", "{\"session\":{}}");
        environment.Runner.RestoreData = seed;

        await environment.SayAsync("第一问", null);
        await environment.SayAsync("第二问", null);

        LlmExecutionParameters first = Assert.IsType<LlmExecutionParameters>(
            environment.Runner.StartedTargets[0].Parameters);
        LlmExecutionParameters second = Assert.IsType<LlmExecutionParameters>(
            environment.Runner.StartedTargets[1].Parameters);

        Assert.Null(first.ConversationSeed);
        Assert.Equal(seed, second.ConversationSeed);
    }

    /// <summary>语言变化不改变已有会话，新会话使用新语言。</summary>
    [Fact]
    public async Task Language_change_keeps_the_existing_session_and_applies_to_a_new_session()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复";

        await environment.SayAsync("第一问", null);
        string firstOwner = environment.Runner.StartedCode[0].Request.Owner.Value;
        environment.Settings.Set("Assistant:Language", "English").ThrowIfError();
        await environment.SayAsync("第二问", null);
        environment.Dialogue.Create().ThrowIfError();
        await environment.SayAsync("第三问", null);

        Assert.Equal(firstOwner, environment.Runner.StartedCode[1].Request.Owner.Value);
        Assert.NotEqual(firstOwner, environment.Runner.StartedCode[2].Request.Owner.Value);
    }

    /// <summary>模型变化不改变已有会话，新会话使用新模型。</summary>
    [Fact]
    public async Task Model_change_keeps_the_existing_session_and_applies_to_a_new_session()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复";

        await environment.SayAsync("第一问", null);
        string firstOwner = environment.Runner.StartedCode[0].Request.Owner.Value;
        environment.Catalog.AddModel(
            ModelName.Create("second").Value,
            ProviderName.Create("test").Value,
            ProviderModelName.Create("second").Value).ThrowIfError();
        environment.Models.Select(ModelName.Create("second").Value).ThrowIfError();
        await environment.SayAsync("第二问", null);
        environment.Dialogue.Create().ThrowIfError();
        await environment.SayAsync("第三问", null);

        Assert.Equal(
            ["fake", "fake", "second"],
            environment.Runner.StartedTargets
                .Select(target => Assert.IsType<LlmExecutionParameters>(target.Parameters).Model));
        Assert.Equal(firstOwner, environment.Runner.StartedCode[1].Request.Owner.Value);
        Assert.NotEqual(firstOwner, environment.Runner.StartedCode[2].Request.Owner.Value);
    }

    [Fact]
    public async Task Sessions_keep_separate_tasks_and_modes()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复";
        DialogueSessionInfo first = environment.Dialogue.Create().ThrowIfError();
        await environment.Dialogue.SendAsync(first.Id, "第一问", null);
        environment.Dialogue.SelectMode(first.Id, DialogueMode.Edit).ThrowIfError();
        DialogueSessionInfo second = environment.Dialogue.Create().ThrowIfError();
        await environment.Dialogue.SendAsync(second.Id, "第二问", null);

        Assert.Equal(2, environment.Dialogue.Sessions.Count);
        Assert.Equal(DialogueMode.Edit, environment.Dialogue.Sessions.Single(session => session.Id == first.Id).Mode);
        Assert.Equal(DialogueMode.Read, environment.Dialogue.Sessions.Single(session => session.Id == second.Id).Mode);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task A_running_session_rejects_a_second_send()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复";
        environment.Runner.Gate.Hold(NodePath.Of("回话"));
        DialogueSessionInfo session = environment.Dialogue.Create().ThrowIfError();

        Task<ErrorOr<Success>> running = environment.Dialogue.SendAsync(session.Id, "第一问", null);
        await environment.Runner.Gate.Entered(NodePath.Of("回话"));

        ErrorOr<Success> second = await environment.Dialogue.SendAsync(session.Id, "第二问", null);

        Assert.True(second.IsError);
        Assert.Contains("正在执行", ErrorText(second));

        environment.Runner.Gate.ClearHolds();
        Assert.False((await running.WaitAsync(TestContext.Current.CancellationToken)).IsError);
    }

    [Fact]
    public async Task The_first_input_becomes_the_session_title()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复";
        DialogueSessionInfo session = environment.Dialogue.Create().ThrowIfError();

        await environment.Dialogue.SendAsync(session.Id, "帮我看看 README", null);

        Assert.Equal(
            "帮我看看 README",
            environment.Dialogue.Sessions.Single(candidate => candidate.Id == session.Id).Title);
    }

    [Fact]
    public async Task Deleting_a_session_leaves_the_other_sessions()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复";
        DialogueSessionInfo first = environment.Dialogue.Create().ThrowIfError();
        DialogueSessionInfo second = environment.Dialogue.Create().ThrowIfError();
        await environment.Dialogue.SendAsync(first.Id, "第一问", null);
        await environment.Dialogue.SendAsync(second.Id, "第二问", null);

        (await environment.Dialogue.DeleteAsync(first.Id, TestContext.Current.CancellationToken)).ThrowIfError();

        DialogueSessionInfo remaining = Assert.Single(environment.Dialogue.Sessions);
        Assert.Equal(second.Id, remaining.Id);
    }

    [Fact]
    public async Task Deleting_the_active_session_selects_the_most_recent_remaining_one()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复";
        DialogueSessionInfo first = environment.Dialogue.Create().ThrowIfError();
        await environment.Dialogue.SendAsync(first.Id, "第一问", null);
        DialogueSessionInfo second = environment.Dialogue.Create().ThrowIfError();
        await environment.Dialogue.SendAsync(second.Id, "第二问", null);

        (await environment.Dialogue.DeleteAsync(second.Id, TestContext.Current.CancellationToken)).ThrowIfError();

        Assert.Equal(first.Id, environment.Dialogue.Active?.Id);
    }

    [Fact]
    public async Task Renaming_a_session_changes_its_title()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        DialogueSessionInfo session = environment.Dialogue.Create().ThrowIfError();

        environment.Dialogue.Rename(session.Id, "排查构建失败").ThrowIfError();

        Assert.Equal("排查构建失败", Assert.Single(environment.Dialogue.Sessions).Title);
    }

    [Fact]
    public async Task A_finished_turn_reports_through_the_host_event()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复";
        List<DialogueTurnNotice> notices = [];
        environment.Dialogue.TurnCompleted += notices.Add;
        DialogueSessionInfo session = environment.Dialogue.Create().ThrowIfError();

        await environment.Dialogue.SendAsync(session.Id, "你好", null);

        DialogueTurnNotice notice = Assert.Single(notices);
        Assert.Equal(session.Id, notice.Session);
        Assert.True(notice.Succeeded);
    }

    [Fact]
    public async Task An_unreadable_restore_snapshot_leaves_the_execution_in_the_history_tail()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复";
        environment.Runner.RestoreData = new ExecutionRestoreState("unknown", "{}");
        DialogueSessionInfo session = environment.Dialogue.Create().ThrowIfError();
        await environment.Dialogue.SendAsync(session.Id, "第一问", null);

        ErrorOr<DialogueHistory> history = await environment.Dialogue.ReadHistoryAsync(
            session.Id,
            TestContext.Current.CancellationToken);

        Assert.False(history.IsError, ErrorText(history));
        Assert.Empty(history.Value.Conversation.Messages);
        Assert.Single(history.Value.Tail);
        Assert.NotEmpty(history.Value.Errors);
    }

    [Fact]
    public async Task Closing_the_assistant_writes_session_records()
    {
        using TestEnvironment environment = TestEnvironment.Create(persistTasks: true);
        environment.Runner.CodeReply = _ => "回复";
        DialogueSessionInfo session = environment.Dialogue.Create().ThrowIfError();
        await environment.Dialogue.SendAsync(session.Id, "第一问", null);

        await environment.Dialogue.CloseAsync();

        string records = KuroePaths.At(environment.Root).AssistantRecordsDirectory;
        Assert.True(Directory.Exists(records));
        Assert.NotEmpty(Directory.EnumerateFiles(records));
    }

    [Fact]
    public async Task Deleting_a_persisted_session_removes_its_record()
    {
        using TestEnvironment environment = TestEnvironment.Create(persistTasks: true);
        DialogueSessionInfo session = environment.Dialogue.Create().ThrowIfError();
        string record = Path.Combine(
            KuroePaths.At(environment.Root).AssistantRecordsDirectory,
            $"{session.Id.Value}.json");
        Assert.True(File.Exists(record));

        (await environment.Dialogue.DeleteAsync(session.Id, TestContext.Current.CancellationToken)).ThrowIfError();

        Assert.False(File.Exists(record));
    }

    [Fact]
    public async Task A_new_instance_restores_sessions_from_the_assistant_records()
    {
        string root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));
        TestEnvironment first = TestEnvironment.Create(persistTasks: true, root: root);

        try
        {
            first.Runner.CodeReply = _ => "回复";
            DialogueSessionInfo session = first.Dialogue.Create().ThrowIfError();
            await first.Dialogue.SendAsync(session.Id, "第一问", null);
            await first.Tasks.ShutdownAsync();
            await first.Dialogue.CloseAsync();

            using TestEnvironment second = TestEnvironment.Create(seedCatalog: false, persistTasks: true, root: root);
            var report = second.Dialogue.Restore();

            Assert.Empty(report.Failures);
            DialogueSessionInfo restored = Assert.Single(second.Dialogue.Sessions);
            Assert.Equal("第一问", restored.Title);
        }
        finally
        {
            first.Dispose();

            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // 第二个实例可能已经清理临时目录
            }
        }
    }

    [Fact]
    public async Task Dialogue_round_emits_no_task_notice()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复你好";
        List<ExecutionNotice> notices = [];
        environment.Registry.Notified += notices.Add;

        ErrorOr<Success> reply = await environment.SayAsync("你好", null);

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
            ErrorOr<Success> reply = await environment.SayAsync($"第{index}问", new ProcessObserver(observer.Content, observer.Entry));

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

        ErrorOr<Success> reply = await environment.SayAsync("你好", new ProcessObserver(observer.Content, observer.Entry));

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

        ErrorOr<Success> reply = await environment.SayAsync("你好", new ProcessObserver(observer.Content, observer.Entry));

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

        ErrorOr<Success> reply = await environment.SayAsync("你好", new ProcessObserver(observer.Content, observer.Entry));

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

        Task<ErrorOr<Success>> running = environment.SayAsync("第一问", null);
        await environment.Runner.Gate.Entered(NodePath.Of("回话"));
        IExecution execution = Assert.Single(environment.Runner.StartedCode);
        ITaskFlowContext owner = environment.Executions.FindOwned(execution.Id).ThrowIfError().Owner;

        lock (owner.Gate)
        {
            owner.Pause().ThrowIfError();
            environment.Driver.Pause(owner);
        }

        await WaitUntilAsync(() => !execution.IsLive);
        Assert.Equal(Kuroe.Execution.Shared.ExecutionState.Interrupted, execution.State);
        Assert.True((await running.WaitAsync(TestContext.Current.CancellationToken)).IsError);

        lock (owner.Gate)
        {
            owner.Resume().ThrowIfError();
            environment.Driver.ResumeTask(owner);
        }

        environment.Runner.Gate.ClearHolds();

        ErrorOr<Success> reply = await environment.SayAsync("第二问", null);

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

    private static string ErrorText<T>(ErrorOr<T> result) =>
        result.IsError ? string.Join("；", result.ErrorsOrEmptyList.Select(error => error.Description)) : string.Empty;

    private static LlmParameters Parameters(TestEnvironment environment, int round)
    {
        LlmExecutionParameters execution = Assert.IsType<LlmExecutionParameters>(
            environment.Runner.StartedTargets[round].Parameters);

        return new LlmParameters
        {
            Prompt = execution.Prompt,
            Tools = [.. execution.Tools.Select(tool => new ToolPath(tool))],
            SystemPrompt = execution.SystemPrompt,
            ToolPorts = execution.ToolPorts,
        };
    }

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
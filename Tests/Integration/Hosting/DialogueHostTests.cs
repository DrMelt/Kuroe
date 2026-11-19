using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Assistant.Dialogue;
using Kuroe.Configuration.Files;
using Kuroe.Control.Shared;
using Kuroe.FlowDescription.Shared.Model;
using Kuroe.Tasks.Shared;
using Kuroe.Tool;
using Kuroe.Node.Llm;
using Kuroe.Runner.Llm;
using Kuroe.Pending;
using Xunit;
using Kuroe.Execution.Shared.Notice;
using Kuroe.Execution.Shared.Restore;
using Kuroe.Execution.Shared.State;
using ChangeWaiter = Kuroe.Flow.TestSupport.ChangeWaiter;

namespace Kuroe.Integration.Tests.Hosting;

/// <summary>内置助手会话宿主：无模型时报模型未选择、一轮落到代码流程、多轮在同一会话上接续、语言与模型只影响新建会话、会话隔离与忙时拒绝、历史与任务域生命周期。</summary>
public sealed class DialogueHostTests
{
    [Fact(DisplayName = "未选择模型时报出模型错误")]
    public async Task Without_model_reports_the_model_error()
    {
        using TestEnvironment environment = TestEnvironment.Create(seedCatalog: false);

        ErrorOr<Success> reply = await environment.SayAsync("你好", null);

        Assert.True(reply.IsError);
        Assert.Equal("当前未选择模型。", reply.ErrorsText());
    }

    [Fact(DisplayName = "选择模型后通过对话任务回复")]
    public async Task With_model_replies_through_dialogue_task()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复你好";
        RecordingOutput observer = new();

        ErrorOr<Success> reply = await environment.SayAsync("你好", new ProcessObserver(observer.Content, observer.Entry));

        Assert.False(reply.IsError, reply.ErrorsText());
        Assert.Equal(["回复你好"], observer.Texts);
    }

    [Fact(DisplayName = "多轮在同一对话任务上接续")]
    public async Task Rounds_continue_on_the_same_dialogue_task()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        int round = 0;
        environment.Runner.CodeReply = _ => $"回复{++round}";
        RecordingOutput observer = new();

        ErrorOr<Success> first = await environment.SayAsync("第一问", new ProcessObserver(observer.Content, observer.Entry));
        ErrorOr<Success> second = await environment.SayAsync("第二问", new ProcessObserver(observer.Content, observer.Entry));

        Assert.False(first.IsError, first.ErrorsText());
        Assert.False(second.IsError, second.ErrorsText());
        Assert.Equal(["回复1", "回复2"], observer.Texts);
    }

    [Fact(DisplayName = "切换模式保留对话任务并改变下一轮工具")]
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

    [Fact(DisplayName = "建立会话时给出的模式作用于首轮")]
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

    [Fact(DisplayName = "下一轮从上次执行会话快照继续")]
    public async Task Rounds_continue_from_the_previous_execution_session_snapshot()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复";
        ExecutionRestoreState seed = new("fake", "{\"session\":{}}");
        environment.Runner.RestoreData = seed;

        await environment.SayAsync("第一问", null);
        await environment.SayAsync("第二问", null);

        LlmExecutionParameters first = Assert.IsType<LlmExecutionParameters>(
            environment.Runner.StartedParameters[0]);
        LlmExecutionParameters second = Assert.IsType<LlmExecutionParameters>(
            environment.Runner.StartedParameters[1]);

        Assert.Null(first.ConversationSeed);
        Assert.Equal(seed, second.ConversationSeed);
    }

    [Fact(DisplayName = "语言变化不改变已有会话，新会话使用新语言")]
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

    [Fact(DisplayName = "模型变化不改变已有会话，新会话使用新模型")]
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
            environment.Runner.StartedParameters
                .Select(parameters => Assert.IsType<LlmExecutionParameters>(parameters).Model));
        Assert.Equal(firstOwner, environment.Runner.StartedCode[1].Request.Owner.Value);
        Assert.NotEqual(firstOwner, environment.Runner.StartedCode[2].Request.Owner.Value);
    }

    [Fact(DisplayName = "各会话保持独立的任务与模式")]
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

    [Fact(DisplayName = "运行中的会话拒绝第二次发送")]
    public async Task A_running_session_rejects_a_second_send()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复";
        environment.Runner.Gate.Hold(NodePath.Of("回话"));
        DialogueSessionInfo session = environment.Dialogue.Create().ThrowIfError();

        Task<ErrorOr<Success>> running = environment.Dialogue.SendAsync(session.Id, "第一问", null);
        await environment.Runner.Gate.Entered(NodePath.Of("回话"));

        ErrorOr<Success> second = await environment.Dialogue.SendAsync(session.Id, "第二问", null);

        Assert.True(second.IsError, "第二问应被拒绝。");
        Assert.Contains("正在执行", second.ErrorsText());

        environment.Runner.Gate.ClearHolds();
        ErrorOr<Success> first = await running.WaitAsync(TestContext.Current.CancellationToken);
        Assert.False(first.IsError, first.ErrorsText());
    }

    [Fact(DisplayName = "首条输入成为会话标题")]
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

    [Fact(DisplayName = "删除会话不影响其他会话")]
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

    [Fact(DisplayName = "删除当前会话后选中最近剩余的会话")]
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

    [Fact(DisplayName = "重命名会话改变其标题")]
    public async Task Renaming_a_session_changes_its_title()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        DialogueSessionInfo session = environment.Dialogue.Create().ThrowIfError();

        environment.Dialogue.Rename(session.Id, "排查构建失败").ThrowIfError();

        Assert.Equal("排查构建失败", Assert.Single(environment.Dialogue.Sessions).Title);
    }

    [Fact(DisplayName = "完成的一轮通过宿主事件报告")]
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

    [Fact(DisplayName = "无法读取的恢复快照把执行留在历史尾部")]
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

        Assert.False(history.IsError, history.ErrorsText());
        Assert.Empty(history.Value.Conversation.Messages);
        Assert.Single(history.Value.Tail);
        Assert.NotEmpty(history.Value.Errors);
    }

    [Fact(DisplayName = "关闭助手写入会话记录")]
    public async Task Closing_the_assistant_writes_session_records()
    {
        using TestEnvironment environment = TestEnvironment.Create(persistTasks: true);
        environment.Runner.CodeReply = _ => "回复";
        DialogueSessionInfo session = environment.Dialogue.Create().ThrowIfError();
        await environment.Dialogue.SendAsync(session.Id, "第一问", null);

        await environment.Dialogue.CloseAsync();

        string records = KuroePaths.At(environment.Root).AssistantRecordsDirectory;
        Assert.True(Directory.Exists(records), $"{records} 不存在。");
        Assert.NotEmpty(Directory.EnumerateFiles(records));
    }

    [Fact(DisplayName = "删除已持久化的会话同时删除其记录")]
    public async Task Deleting_a_persisted_session_removes_its_record()
    {
        using TestEnvironment environment = TestEnvironment.Create(persistTasks: true);
        DialogueSessionInfo session = environment.Dialogue.Create().ThrowIfError();
        string record = Path.Combine(
            KuroePaths.At(environment.Root).AssistantRecordsDirectory,
            $"{session.Id.Value}.json");
        Assert.True(File.Exists(record), $"{record} 不存在。");

        (await environment.Dialogue.DeleteAsync(session.Id, TestContext.Current.CancellationToken)).ThrowIfError();

        Assert.False(File.Exists(record), $"{record} 不应存在。");
    }

    [Fact(DisplayName = "新实例从助手记录恢复会话")]
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

    [Fact(DisplayName = "对话轮次不发任务通知")]
    public async Task Dialogue_round_emits_no_task_notice()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复你好";
        List<ExecutionNotice> notices = [];
        environment.Registry.Notified += notices.Add;

        ErrorOr<Success> reply = await environment.SayAsync("你好", null);

        Assert.False(reply.IsError, reply.ErrorsText());
        Assert.Empty(notices);
    }

    [Fact(DisplayName = "连续轮次不报告任务收口")]
    public async Task Consecutive_rounds_never_report_settlement()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        int round = 0;
        environment.Runner.CodeReply = _ => $"回复{++round}";
        RecordingOutput observer = new();

        for (int index = 1; index <= 8; index++)
        {
            ErrorOr<Success> reply = await environment.SayAsync($"第{index}问", new ProcessObserver(observer.Content, observer.Entry));

            Assert.False(reply.IsError, reply.ErrorsText());
        }

        Assert.Equal(
            [.. Enumerable.Range(1, 8).Select(index => $"回复{index}")],
            observer.Texts);
        Assert.Single(environment.Runner.StartedCode.Select(execution => execution.Request.Owner).Distinct());
    }

    [Fact(DisplayName = "一轮把工具调用交给观察者")]
    public async Task A_round_hands_tool_calls_to_its_observer()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复你好";
        environment.Runner.CodeEmits = (_, journal) =>
            journal.AppendToolCall("GetLocalTime", "{}", "2026-10-18 19:10:48", false);
        RecordingOutput observer = new();

        ErrorOr<Success> reply = await environment.SayAsync("你好", new ProcessObserver(observer.Content, observer.Entry));

        Assert.False(reply.IsError, reply.ErrorsText());
        JournalEntry entry = Assert.Single(observer.Entries);
        Assert.Equal("工具", entry.Label);
        Assert.Equal("GetLocalTime {}\n2026-10-18 19:10:48", entry.Text);
    }

    [Fact(DisplayName = "一轮记录节点条目并流式输出内容")]
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

        Assert.False(reply.IsError, reply.ErrorsText());
        Assert.Equal(["增量", "回复你好"], observer.Texts);

        IExecution round = Assert.Single(environment.Runner.StartedCode);
        List<JournalEntry> entries = [.. round.Snapshot().Journal];
        Assert.Contains(entries, entry => entry.Label == "输入" && entry.Text == "问题");
        Assert.Contains(entries, entry => entry.Text.Contains("增量", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "一轮把推理内容交给观察者")]
    public async Task A_round_hands_reasoning_to_its_observer()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => string.Empty;
        environment.Runner.CodeEmits = (_, journal) =>
            journal.AppendContent(ProcessContentKind.Reasoning, "先想");
        RecordingOutput observer = new();

        ErrorOr<Success> reply = await environment.SayAsync("你好", new ProcessObserver(observer.Content, observer.Entry));

        Assert.False(reply.IsError, reply.ErrorsText());
        Assert.Equal(["先想"], observer.Reasonings);
        Assert.Empty(observer.Texts);
    }

    [Fact(DisplayName = "已收口对话的错误按展示名报出状态")]
    public void Settled_dialogue_error_names_the_state_in_its_display_form()
    {
        Error error = PendingErrors.DialogueSettled(TaskState.Blocked);

        Assert.Equal("对话已阻塞，无法继续对话。", error.Description);
    }

    [Fact(DisplayName = "暂停中断运行中的对话轮次恢复后继续")]
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

        await WaitUntilSettledAsync(execution);
        Assert.Equal(Kuroe.Execution.Shared.State.ExecutionState.Interrupted, execution.State);
        Assert.True((await running.WaitAsync(TestContext.Current.CancellationToken)).IsError);

        lock (owner.Gate)
        {
            owner.Resume().ThrowIfError();
            environment.Driver.ResumeTask(owner);
        }

        environment.Runner.Gate.ClearHolds();

        ErrorOr<Success> reply = await environment.SayAsync("第二问", null);

        Assert.False(reply.IsError, reply.ErrorsText());
        Assert.Equal(2, environment.Runner.StartedCode.Count);
    }

    /// <summary>等执行收口，靠执行的变更通知唤醒。</summary>
    private static async Task WaitUntilSettledAsync(IExecution execution)
    {
        ChangeWaiter waiter = new();
        void OnChanged() => waiter.Raise();

        execution.Changed += OnChanged;
        try
        {
            await waiter.UntilAsync(() => !execution.IsLive);
        }
        finally
        {
            execution.Changed -= OnChanged;
        }
    }

    private static LlmParameters Parameters(TestEnvironment environment, int round)
    {
        LlmExecutionParameters execution = Assert.IsType<LlmExecutionParameters>(
            environment.Runner.StartedParameters[round]);

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
using ErrorOr;
using Kuroe.Node.Llm;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Assistant.Dialogue;
using Kuroe.Pending;
using Xunit;

namespace Kuroe.Tests;

/// <summary>常驻对话任务的管理层宿主：无模型时报模型未选择、一轮对话落到任务管线、多轮在同一任务上接续、语言变化后重置才按新系统指令重建、回复文本经过程播发送给观察者。</summary>
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
        environment.Runner.Reply = _ => "回复你好";
        RecordingOutput observer = new();

        ErrorOr<Success> reply = await environment.Dialogue.ReplyAsync("你好", observer.Text, observer.Entry);

        Assert.False(reply.IsError, ErrorText(reply));
        Assert.Equal(["回复你好"], observer.Texts);
    }

    [Fact]
    public async Task Rounds_continue_on_the_same_dialogue_task()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.Reply = execution => $"回复{execution.Input.ExecutionCount}";
        RecordingOutput observer = new();

        ErrorOr<Success> first = await environment.Dialogue.ReplyAsync("第一问", observer.Text, observer.Entry);
        ErrorOr<Success> second = await environment.Dialogue.ReplyAsync("第二问", observer.Text, observer.Entry);

        Assert.False(first.IsError, ErrorText(first));
        Assert.False(second.IsError, ErrorText(second));
        Assert.Equal(["回复1", "回复2"], observer.Texts);
    }

    [Fact]
    public async Task Language_change_waits_for_reset_before_rebuilding()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        List<string> prompts = [];
        environment.Runner.Reply = execution =>
        {
            prompts.Add(Assert.IsType<LlmNodeType>(execution.Input.Type).SystemPrompt()!);
            return $"回复{execution.Input.ExecutionCount}";
        };

        ErrorOr<Success> first = await environment.Dialogue.ReplyAsync("第一问", null);
        environment.Settings.Set("Assistant:Language", "English").ThrowIfError();
        ErrorOr<Success> second = await environment.Dialogue.ReplyAsync("第二问", null);
        environment.Dialogue.Reset();
        ErrorOr<Success> third = await environment.Dialogue.ReplyAsync("第三问", null);

        Assert.False(first.IsError, ErrorText(first));
        Assert.False(second.IsError, ErrorText(second));
        Assert.False(third.IsError, ErrorText(third));
        Assert.Equal(3, prompts.Count);
        Assert.Contains("使用「中文」语言母语者语言风格回答。", prompts[1], StringComparison.Ordinal);
        Assert.Contains("使用「English」语言母语者语言风格回答。", prompts[2], StringComparison.Ordinal);
    }

    /// <summary>回话节点只声明 Signal 输出端口，一轮回答仍要交回这个端口，未交回即不推进。</summary>
    [Fact]
    public async Task A_round_requires_its_port_submission()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.Reply = execution => $"回复{execution.Input.ExecutionCount}";

        ErrorOr<Success> first = await environment.Dialogue.ReplyAsync("第一问", null);

        Assert.False(first.IsError, ErrorText(first));

        environment.Runner.SubmitsPorts = false;
        ErrorOr<Success> second = await environment.Dialogue.ReplyAsync("第二问", null);

        Assert.True(second.IsError);
        Assert.Contains("未收口", ErrorText(second), StringComparison.Ordinal);
    }

    /// <summary>对话轮次阻塞后，下一次输入先停止旧任务并清出任务域，再建新任务。</summary>
    [Fact]
    public async Task A_blocked_dialogue_task_is_discarded_before_rebuilding()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.Reply = _ => "回复你好";
        environment.Runner.SubmitsPorts = false;

        ErrorOr<Success> blocked = await environment.Dialogue.ReplyAsync("第一问", null);
        IExecution stale = Assert.Single(environment.Runner.Started);

        environment.Runner.SubmitsPorts = true;
        ErrorOr<Success> reply = await environment.Dialogue.ReplyAsync("第二问", null);

        Assert.True(blocked.IsError);
        Assert.False(reply.IsError, ErrorText(reply));
        Assert.True(environment.Executions.FindOwned(stale.Id).IsError);
    }

    /// <summary>回话节点每轮各自交回自己的「完成」，轮次之间不重复提交。</summary>
    [Fact]
    public async Task Each_round_submits_its_own_port_output()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.Reply = execution => $"回复{execution.Input.ExecutionCount}";

        ErrorOr<Success> first = await environment.Dialogue.ReplyAsync("第一问", null);
        ErrorOr<Success> second = await environment.Dialogue.ReplyAsync("第二问", null);

        Assert.False(first.IsError, ErrorText(first));
        Assert.False(second.IsError, ErrorText(second));
        Assert.Contains(environment.Runner.Submissions,
            text => text.Contains("已记录 1 个端口的产出。本轮产出已齐备。", StringComparison.Ordinal));
        Assert.DoesNotContain(environment.Runner.Submissions,
            text => text.Contains("无需重复提交", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Dialogue_round_emits_no_task_notice()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.Reply = _ => "回复你好";
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
        environment.Runner.Reply = execution => $"回复{execution.Input.ExecutionCount}";
        RecordingOutput observer = new();

        for (int round = 1; round <= 8; round++)
        {
            ErrorOr<Success> reply = await environment.Dialogue.ReplyAsync($"第{round}问", observer.Text, observer.Entry);

            Assert.False(reply.IsError, ErrorText(reply));
        }

        Assert.Equal(8, observer.Texts.Count);
        Assert.Equal("回复8", observer.Texts[^1]);
    }

    [Fact]
    public async Task A_round_hands_tool_calls_to_its_observer()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.Reply = _ => "回复你好";
        environment.Runner.Emits = (_, journal) =>
            journal.AppendToolCall("GetLocalTime", "{}", "2026-10-18 19:10:48", false);
        RecordingOutput observer = new();

        ErrorOr<Success> reply = await environment.Dialogue.ReplyAsync("你好", observer.Text, observer.Entry);

        Assert.False(reply.IsError, ErrorText(reply));
        JournalEntry entry = Assert.Single(observer.Entries);
        Assert.Equal("工具", entry.Label);
        Assert.Equal("GetLocalTime {}\n结果 2026-10-18 19:10:48", entry.Text);
    }

    [Fact]
    public async Task A_round_records_node_entries_and_streams_text()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.Reply = _ => "回复你好";
        environment.Runner.Emits = (_, journal) =>
        {
            journal.AppendPrompt("问题");
            journal.AppendText("增量");
        };
        RecordingOutput observer = new();

        ErrorOr<Success> reply = await environment.Dialogue.ReplyAsync("你好", observer.Text, observer.Entry);

        Assert.False(reply.IsError, ErrorText(reply));
        Assert.Equal(["增量", "回复你好"], observer.Texts);

        IExecution round = Assert.Single(environment.Runner.Started);
        List<JournalEntry> entries = [.. round.Snapshot().Journal];
        Assert.Contains(entries, entry => entry.Label == "输入" && entry.Text == "问题");
        Assert.Contains(entries, entry => entry.Text.Contains("增量", StringComparison.Ordinal));
    }

    [Fact]
    public void Settled_dialogue_error_names_the_state_in_its_display_form()
    {
        Error error = PendingErrors.DialogueSettled(TaskState.Blocked);

        Assert.Equal("对话已阻塞，无法继续对话。", error.Description);
    }

    private static string ErrorText(ErrorOr<Success> result) =>
        result.IsError ? string.Join("；", result.ErrorsOrEmptyList.Select(error => error.Description)) : string.Empty;

    /// <summary>记下收到的过程，供断言。</summary>
    private sealed class RecordingOutput
    {
        public List<string> Texts { get; } = [];

        public List<JournalEntry> Entries { get; } = [];

        public void Text(string delta) => Texts.Add(delta);

        public void Entry(JournalEntry entry) => Entries.Add(entry);
    }
}

using ErrorOr;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Shared.Workflows;
using Kuroe.Workflows.Execution;
using Kuroe.Flow.Execution.Tasks;
using Kuroe.Waiting;
using Kuroe.Workflows.Flows;
using Xunit;

namespace Kuroe.Tests;

/// <summary>常驻对话任务的管理层宿主：无模型引导、一轮对话落到任务管线、多轮在同一任务上接续、回合失败交回本轮 run 的判定。</summary>
public sealed class DialogueHostTests
{
    [Fact]
    public async Task Without_model_returns_fixed_guidance()
    {
        using KuroeHarness harness = KuroeHarness.Create(seedCatalog: false);

        ErrorOr<DialogueReply> reply = await harness.Dialogue.ReplyAsync("你好", null, CancellationToken.None);

        Assert.False(reply.IsError);
        Assert.Equal(DialogueHost.NoModelGuidance, reply.Value.Text);
    }

    [Fact]
    public async Task With_model_replies_through_dialogue_task()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.Output = _ => "回复你好";

        ErrorOr<DialogueReply> reply = await harness.Dialogue.ReplyAsync("你好", null, CancellationToken.None);

        Assert.False(reply.IsError, ErrorText(reply));
        Assert.Equal("回复你好", reply.Value.Text);
    }

    [Fact]
    public async Task Rounds_continue_on_the_same_dialogue_task()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.Output = run => $"回复{run.Context.ExecutionCount}";

        ErrorOr<DialogueReply> first = await harness.Dialogue.ReplyAsync("第一问", null, CancellationToken.None);
        ErrorOr<DialogueReply> second = await harness.Dialogue.ReplyAsync("第二问", null, CancellationToken.None);

        Assert.False(first.IsError, ErrorText(first));
        Assert.False(second.IsError, ErrorText(second));
        Assert.Equal("回复1", first.Value.Text);
        Assert.Equal("回复2", second.Value.Text);
    }

    [Fact]
    public async Task Uncollected_round_reports_the_failure()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.SubmitsPorts = false;

        ErrorOr<DialogueReply> reply = await harness.Dialogue.ReplyAsync("你好", null, CancellationToken.None);

        Assert.True(reply.IsError);
        string text = ErrorText(reply);
        Assert.Contains("未收口", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Blocked", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dialogue_round_emits_no_task_notice()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.Output = _ => "回复你好";
        List<ExecutionNotice> notices = [];
        harness.Registry.Notified += notices.Add;

        ErrorOr<DialogueReply> reply = await harness.Dialogue.ReplyAsync("你好", null, CancellationToken.None);

        Assert.False(reply.IsError, ErrorText(reply));
        Assert.Empty(notices);
    }

    [Fact]
    public async Task Each_round_submits_its_own_port_output()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.Output = run => $"回复{run.Context.ExecutionCount}";
        harness.Executor.PortValuesJsonFor =
            run => $"{{ \"{DialogueFlow.ReplyPort.Value}\": \"第{run.Context.ExecutionCount}轮产出\" }}";

        ErrorOr<DialogueReply> first = await harness.Dialogue.ReplyAsync("第一问", null, CancellationToken.None);
        ErrorOr<DialogueReply> second = await harness.Dialogue.ReplyAsync("第二问", null, CancellationToken.None);

        Assert.False(first.IsError, ErrorText(first));
        Assert.False(second.IsError, ErrorText(second));
        Assert.DoesNotContain(harness.Executor.Submissions, text => text.Contains("无需重复提交", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_round_without_its_own_submission_does_not_settle()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.Output = run => $"回复{run.Context.ExecutionCount}";

        ErrorOr<DialogueReply> first = await harness.Dialogue.ReplyAsync("第一问", null, CancellationToken.None);
        Assert.False(first.IsError, ErrorText(first));

        harness.Executor.SubmitsPorts = false;
        ErrorOr<DialogueReply> second = await harness.Dialogue.ReplyAsync("第二问", null, CancellationToken.None);

        Assert.True(second.IsError);
        Assert.Contains("未收口", ErrorText(second), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Consecutive_rounds_never_report_settlement()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.Output = run => $"回复{run.Context.ExecutionCount}";

        for (int round = 1; round <= 8; round++)
        {
            ErrorOr<DialogueReply> reply = await harness.Dialogue.ReplyAsync($"第{round}问", null, CancellationToken.None);

            Assert.False(reply.IsError, ErrorText(reply));
            Assert.Equal($"回复{round}", reply.Value.Text);
        }
    }

    [Fact]
    public void Settled_dialogue_error_names_the_state_in_its_display_form()
    {
        Error error = WaitErrors.Settled(new TaskId(1), TaskState.Blocked);

        Assert.Equal("任务 #1 已阻塞，无法继续对话。", error.Description);
    }

    private static string ErrorText(ErrorOr<DialogueReply> result) =>
        result.IsError ? string.Join("；", result.ErrorsOrEmptyList.Select(error => error.Description)) : string.Empty;
}

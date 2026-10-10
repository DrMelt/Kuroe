using ErrorOr;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
using Kuroe.Workflows.TaskExecution;
using Xunit;

namespace Kuroe.Tests;

/// <summary>常驻对话任务的管理层宿主：无模型引导、一轮对话落到任务管线、多轮在同一任务上接续。</summary>
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

    private static string ErrorText(ErrorOr<DialogueReply> result) =>
        result.IsError ? string.Join("；", result.ErrorsOrEmptyList.Select(error => error.Description)) : string.Empty;
}

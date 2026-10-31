using ErrorOr;
using Kuroe.Node.Llm.Executions;
using Xunit;

namespace Kuroe.Node.Llm.Tests.Executions;

/// <summary>ExecutionRunner 的补发循环：产出契约缺项时在同一会话里补发，补满上限即止，失败不再补发。</summary>
public sealed class ExecutionRunnerTests
{
    [Fact]
    public async Task Missing_nothing_asks_once()
    {
        List<string> asked = [];

        ErrorOr<Success> outcome = await ExecutionRunner.AskWithReminders(
            text => Ask(asked, text),
            "本轮指令",
            () => null);

        Assert.Equal(["本轮指令"], asked);
        Assert.False(outcome.IsError);
    }

    [Fact]
    public async Task Missing_output_asks_until_the_limit()
    {
        List<string> asked = [];

        ErrorOr<Success> outcome = await ExecutionRunner.AskWithReminders(
            text => Ask(asked, text),
            "本轮指令",
            () => "补交要求");

        Assert.Equal(["本轮指令", "补交要求", "补交要求"], asked);
        Assert.False(outcome.IsError);
    }

    [Fact]
    public async Task Filled_output_stops_asking()
    {
        List<string> asked = [];
        int reminders = 0;

        ErrorOr<Success> outcome = await ExecutionRunner.AskWithReminders(
            text => Ask(asked, text),
            "本轮指令",
            () => ++reminders == 1 ? "补交要求" : null);

        Assert.Equal(["本轮指令", "补交要求"], asked);
        Assert.False(outcome.IsError);
    }

    [Fact]
    public async Task Failed_reply_is_not_reminded()
    {
        List<string> asked = [];

        ErrorOr<Success> outcome = await ExecutionRunner.AskWithReminders(
            text => Failed(asked, text),
            "本轮指令",
            () => "补交要求");

        Assert.Equal(["本轮指令"], asked);
        Assert.True(outcome.IsError);
    }

    private static Task<ErrorOr<Success>> Ask(List<string> asked, string text)
    {
        asked.Add(text);

        return Task.FromResult<ErrorOr<Success>>(Result.Success);
    }

    private static Task<ErrorOr<Success>> Failed(List<string> asked, string text)
    {
        asked.Add(text);

        return Task.FromResult<ErrorOr<Success>>(Error.Failure("Execution.Failed", "模型失败"));
    }
}

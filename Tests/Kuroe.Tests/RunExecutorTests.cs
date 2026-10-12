using ErrorOr;
using Kuroe.Executions.Runs;
using Xunit;

namespace Kuroe.Tests;

/// <summary>RunExecutor 的补发循环：产出契约缺项时在同一会话里补发，补满上限即止，失败不再补发。</summary>
public sealed class RunExecutorTests
{
    [Fact]
    public async Task Missing_nothing_asks_once()
    {
        List<string> asked = [];

        ErrorOr<string> reply = await RunExecutor.AskWithReminders(
            text => Ask(asked, text),
            "本轮指令",
            () => null);

        Assert.Equal(["本轮指令"], asked);
        Assert.Equal("回复", reply.Value);
    }

    [Fact]
    public async Task Missing_output_asks_until_the_limit()
    {
        List<string> asked = [];

        ErrorOr<string> reply = await RunExecutor.AskWithReminders(
            text => Ask(asked, text),
            "本轮指令",
            () => "补交要求");

        Assert.Equal(["本轮指令", "补交要求", "补交要求"], asked);
        Assert.Equal("回复", reply.Value);
    }

    [Fact]
    public async Task Filled_output_stops_asking()
    {
        List<string> asked = [];
        int reminders = 0;

        ErrorOr<string> reply = await RunExecutor.AskWithReminders(
            text => Ask(asked, text),
            "本轮指令",
            () => ++reminders == 1 ? "补交要求" : null);

        Assert.Equal(["本轮指令", "补交要求"], asked);
        Assert.Equal("回复", reply.Value);
    }

    [Fact]
    public async Task Failed_reply_is_not_reminded()
    {
        List<string> asked = [];

        ErrorOr<string> reply = await RunExecutor.AskWithReminders(
            text => Failed(asked, text),
            "本轮指令",
            () => "补交要求");

        Assert.Equal(["本轮指令"], asked);
        Assert.True(reply.IsError);
    }

    private static Task<ErrorOr<string>> Ask(List<string> asked, string text)
    {
        asked.Add(text);

        return Task.FromResult<ErrorOr<string>>("回复");
    }

    private static Task<ErrorOr<string>> Failed(List<string> asked, string text)
    {
        asked.Add(text);

        return Task.FromResult<ErrorOr<string>>(Error.Failure("Run.Failed", "模型失败"));
    }
}

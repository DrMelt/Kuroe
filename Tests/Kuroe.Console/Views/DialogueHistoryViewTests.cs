using Kuroe.Assistant.Dialogue;
using Kuroe.Console.Views;
using Kuroe.Execution.Shared;
using Xunit;

namespace Kuroe.Console.Tests.Views;

/// <summary>会话历史按用户与助手区块呈现，基础历史只显示文本，尾部执行给出状态。</summary>
public sealed class DialogueHistoryViewTests
{
    [Fact]
    public void Prints_user_and_assistant_text_in_separate_blocks()
    {
        using ConsoleSession cli = new();
        DialogueHistory history = new(
            new ExecutionConversation(
            [
                TextMessage(ExecutionMessageRole.User, "第一问"),
                new ExecutionMessage(
                    ExecutionMessageRole.Assistant,
                    [
                        new ExecutionContentPart(ExecutionContentKind.Reasoning, "先想"),
                        new ExecutionContentPart(ExecutionContentKind.Text, "回复一"),
                    ]),
            ]),
            [],
            []);

        new DialogueHistoryView(cli.Terminal).Print(history);

        string output = cli.Output.Output;
        Assert.Contains("用户", output);
        Assert.Contains("Kuroe 助手", output);
        Assert.Contains("第一问", output);
        Assert.Contains("回复一", output);
        Assert.DoesNotContain("先想", output);
        Assert.DoesNotContain("你：", output);
    }

    [Fact]
    public void Hides_system_and_tool_messages()
    {
        using ConsoleSession cli = new();
        DialogueHistory history = new(
            new ExecutionConversation(
            [
                TextMessage(ExecutionMessageRole.System, "系统提示"),
                TextMessage(ExecutionMessageRole.User, "问题"),
                new ExecutionMessage(
                    ExecutionMessageRole.Assistant,
                    [new ExecutionContentPart(ExecutionContentKind.ToolCall, "file/read")]),
                TextMessage(ExecutionMessageRole.Tool, "工具结果"),
                TextMessage(ExecutionMessageRole.Assistant, "回答"),
            ]),
            [],
            []);

        new DialogueHistoryView(cli.Terminal).Print(history);

        string output = cli.Output.Output;
        Assert.Contains("问题", output);
        Assert.Contains("回答", output);
        Assert.DoesNotContain("系统提示", output);
        Assert.DoesNotContain("file/read", output);
        Assert.DoesNotContain("工具结果", output);
    }

    [Fact]
    public void Tail_execution_shows_input_and_failure_state()
    {
        using ConsoleSession cli = new();
        DialogueHistory history = new(
            ExecutionConversation.Empty,
            [Snapshot("第二问", ExecutionState.Failed, "模型错误")],
            []);

        new DialogueHistoryView(cli.Terminal).Print(history);

        string output = cli.Output.Output;
        Assert.Contains("第二问", output);
        Assert.Contains("〔失败〕模型错误", output);
    }

    [Fact]
    public void A_succeeded_tail_execution_still_gives_its_state()
    {
        using ConsoleSession cli = new();
        DialogueHistory history = new(
            ExecutionConversation.Empty,
            [Snapshot("第三问", ExecutionState.Succeeded)],
            []);

        new DialogueHistoryView(cli.Terminal).Print(history);

        Assert.Contains("〔已完成〕", cli.Output.Output);
    }

    [Fact]
    public void Empty_history_only_gives_a_hint()
    {
        using ConsoleSession cli = new();

        new DialogueHistoryView(cli.Terminal).Print(new DialogueHistory(ExecutionConversation.Empty, [], []));

        string output = cli.Output.Output;
        Assert.Contains("还没有对话。", output);
        Assert.DoesNotContain("会话历史", output);
    }

    [Fact]
    public void Messages_beyond_the_limit_are_omitted_with_a_hint()
    {
        using ConsoleSession cli = new();
        ExecutionMessage[] messages =
        [
            .. Enumerable.Range(0, 41)
                .Select(index => TextMessage(ExecutionMessageRole.User, $"消息{index}")),
        ];
        DialogueHistory history = new(new ExecutionConversation(messages), [], []);

        new DialogueHistoryView(cli.Terminal).Print(history);

        string output = cli.Output.Output;
        Assert.Contains("更早的消息未显示。", output);
        Assert.DoesNotContain("消息0", output);
        Assert.Contains("消息40", output);
    }

    [Fact]
    public void Tail_executions_beyond_the_limit_are_omitted_with_a_hint()
    {
        using ConsoleSession cli = new();
        ExecutionSnapshot[] tail =
        [
            .. Enumerable.Range(0, 21)
                .Select(index => Snapshot($"第{index}问", ExecutionState.Succeeded)),
        ];
        DialogueHistory history = new(ExecutionConversation.Empty, tail, []);

        new DialogueHistoryView(cli.Terminal).Print(history);

        string output = cli.Output.Output;
        Assert.Contains("更早的未完成轮次未显示。", output);
        Assert.DoesNotContain("第0问", output);
        Assert.Contains("第20问", output);
    }

    private static ExecutionMessage TextMessage(ExecutionMessageRole role, string text) =>
        new(role, [new ExecutionContentPart(ExecutionContentKind.Text, text)]);

    private static ExecutionSnapshot Snapshot(string input, ExecutionState state, params string[] failures)
    {
        ExecutionRequest request = new(
            new ExecutionOwnerId("task"),
            new ExecutionTargetId("回话"),
            new ExecutionKind("llm"),
            1,
            [],
            [new ExecutionInputValue(new ExecutionPortName("回答"), input, null)],
            new Dictionary<ExecutionPortName, string>(),
            []);

        return new ExecutionSnapshot(
            new ExecutionId(1),
            request,
            state,
            string.Empty,
            DateTimeOffset.UnixEpoch,
            null,
            null,
            failures,
            [],
            ExecutionUsage.None,
            null,
            ExecutionContract.Empty,
            null,
            null);
    }
}
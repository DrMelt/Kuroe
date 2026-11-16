using Kuroe.Execution.Shared;
using Microsoft.Extensions.AI;
using Xunit;

namespace Kuroe.Runner.Llm.Tests;

/// <summary>本轮请求历史：输入取值按来源与文本写成模型消息。</summary>
public sealed class ExecutionHistoryTests
{
    [Fact]
    public void Execution_source_renders_node_port_and_text()
    {
        ExecutionRequest request = Request(
            [new ExecutionInputValue(
                new ExecutionPortName("计划"),
                "计划产出",
                "节点「整体/制定计划」的端口「计划」产出")]);
        List<ChatMessage> messages = [];

        History(request).LayOut(messages);

        Assert.Equal("节点「整体/制定计划」的端口「计划」产出：\n计划产出", Assert.Single(messages).Text);
    }

    [Fact]
    public void Long_input_is_laid_out_without_truncation()
    {
        string text = new('甲', 3000);
        ExecutionRequest request = Request(
            [new ExecutionInputValue(new ExecutionPortName("正文"), text, null)]);
        List<ChatMessage> messages = [];

        History(request).LayOut(messages);

        Assert.EndsWith(text, Assert.Single(messages).Text);
    }

    [Fact]
    public void Inputs_are_appended_to_the_existing_history()
    {
        ExecutionRequest request = Request(
            [new ExecutionInputValue(new ExecutionPortName("追问"), "继续", null)]);
        List<ChatMessage> messages = [new ChatMessage(ChatRole.Assistant, "上一轮回复")];

        History(request).LayOut(messages);

        Assert.Equal(["上一轮回复", "继续"], messages.Select(message => message.Text));
    }

    private static ExecutionHistory History(ExecutionRequest request) =>
        new(request, new LlmExecutionParameters(null, [], [], new Dictionary<string, IReadOnlyDictionary<string, string>>(), null));

    private static ExecutionRequest Request(IReadOnlyList<ExecutionInputValue> inputs) =>
        new(
            new ExecutionOwnerId("1"),
            new ExecutionTargetId("实施"),
            1,
            [],
            inputs,
            new Dictionary<ExecutionPortName, string>(),
            []);
}

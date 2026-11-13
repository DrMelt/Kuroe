using Kuroe.Execution.Shared;
using Kuroe.Node.Llm.Sessions;
using Microsoft.Extensions.AI;
using Xunit;

namespace Kuroe.Node.Llm.Tests;

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

    private static ExecutionHistory History(ExecutionRequest request) =>
        new(request, new LlmExecutionParameters(null, null, LlmNodeTypes.Kind));

    private static ExecutionRequest Request(IReadOnlyList<ExecutionInputValue> inputs) =>
        new(
            new ExecutionOwnerId("1"),
            new ExecutionTargetId("实施"),
            "Llm",
            1,
            [],
            inputs,
            new Dictionary<ExecutionPortName, string>(),
            []);
}

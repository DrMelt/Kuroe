using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Node.Llm.Sessions;
using Microsoft.Extensions.AI;
using Xunit;

namespace Kuroe.Node.Llm.Tests;

/// <summary>本轮请求历史：输入取值按出处与文本写成模型消息。</summary>
public sealed class ExecutionHistoryTests
{
    [Fact]
    public void Execution_source_renders_node_port_and_text()
    {
        ExecutionInput input = new()
        {
            Task = new TaskId(1),
            Node = NodePath.Of("实施"),
            Type = LlmNodeTypes.Create(null),
            Inputs =
            [
                new InputValue(
                    new PortName("计划"),
                    new ContextValue(
                        new ExecutionSource(new ExecutionId(2), NodePath.Of("整体/制定计划"), new PortName("计划")),
                        "计划产出")),
            ],
        };
        List<ChatMessage> messages = [];

        new ExecutionHistory(input).LayOut(messages);

        Assert.Equal("节点「整体/制定计划」的端口「计划」产出：\n计划产出", Assert.Single(messages).Text);
    }

    [Fact]
    public void Long_input_is_laid_out_without_truncation()
    {
        string text = new('甲', 3000);
        ExecutionInput input = new()
        {
            Task = new TaskId(1),
            Node = NodePath.Of("实施"),
            Type = LlmNodeTypes.Create(null),
            Inputs =
            [
                new InputValue(
                    new PortName("正文"),
                    new ContextValue(new StartSource(new PortName("正文")), text)),
            ],
        };
        List<ChatMessage> messages = [];

        new ExecutionHistory(input).LayOut(messages);

        Assert.EndsWith(text, Assert.Single(messages).Text);
    }
}

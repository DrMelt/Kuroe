using ApiHub.Shared.Models;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Tool;
using Microsoft.Extensions.AI;

namespace Kuroe.Node.Llm.Sessions;

/// <summary>会话历史的来源：决定用哪个模型、历史何时重铺，以及本轮请求的模型侧内容。</summary>
internal interface ISessionHistory
{
    /// <summary>本轮使用的模型，未选择时为空。</summary>
    ModelName? Model { get; }

    /// <summary>当前历史是否已不适用。</summary>
    bool Stale { get; }

    /// <summary>本轮系统指令，没有内容时为空。</summary>
    string? SystemPrompt { get; }

    /// <summary>本轮可用的工具路径，没有能力时为空集合。</summary>
    IReadOnlyList<ToolPath> ToolPaths { get; }

    /// <summary>重铺历史。</summary>
    void LayOut(List<ChatMessage> messages);
}

/// <summary>一次执行的会话：只在来源丢失时重铺一次，执行期间来源不再改变。</summary>
internal sealed class ExecutionHistory(ExecutionInput input) : ISessionHistory
{
    private bool _laidOut;

    public ModelName? Model => (input.Declaration as KuroeNodeDeclaration)?.Model;

    public bool Stale => !_laidOut;

    /// <summary>该执行的系统指令：节点声明的恒定块，置于请求最前。</summary>
    public string? SystemPrompt => (input.Type as LlmNodeType)?.SystemPrompt();

    /// <summary>该执行的能力面：节点声明的白名单与按输出端口附上的契约能力。</summary>
    public IReadOnlyList<ToolPath> ToolPaths => (input.Type as LlmNodeType)?.ToolPaths(input.OutputPorts) ?? [];

    public void LayOut(List<ChatMessage> messages)
    {
        _laidOut = true;

        messages.Clear();
        foreach (ContextValue value in input.Inputs)
        {
            messages.Add(new ChatMessage(ChatRole.User, Text(value)));
        }
    }

    /// <summary>一条取值在请求里的写法：上游产出标明来源节点与端口，输入节点给回答，流程起点的初始输入原样给出。</summary>
    private static string Text(ContextValue value) => value.Origin switch
    {
        ExecutionSource source => $"节点「{source.Node}」的端口「{source.Port}」产出：\n{value.Text}",
        ContainerPortSource source => $"节点「{source.Container}」的端口「{source.Port}」产出：\n{value.Text}",
        InputSource source => $"节点「{source.Node}」的输入：\n{value.Text}",
        DefaultSource source => $"节点「{source.Node}」的端口「{source.Port}」默认值：\n{value.Text}",
        _ => value.Text,
    };
}

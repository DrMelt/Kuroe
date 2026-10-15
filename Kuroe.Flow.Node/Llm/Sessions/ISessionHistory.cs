using ApiHub.Shared.Models;
using Kuroe.Flow.Shared.Execution;
using Microsoft.Extensions.AI;

namespace Kuroe.Flow.Node.Llm.Sessions;

/// <summary>会话历史的来源：决定用哪个模型、历史何时重铺。</summary>
internal interface ISessionHistory
{
    /// <summary>本轮使用的模型，未选择时为空。</summary>
    ModelName? Model { get; }

    /// <summary>当前历史是否已不适用。</summary>
    bool Stale { get; }

    /// <summary>恒定系统指令，作为系统指令置于请求最前，未声明时为空。</summary>
    string? SystemPrompt { get; }

    /// <summary>重铺历史。</summary>
    void LayOut(List<ChatMessage> messages);
}

/// <summary>一次执行的会话：只在来源丢失时重铺一次，执行期间来源不再改变。</summary>
internal sealed class RunHistory(RunContext context) : ISessionHistory
{
    private bool _laidOut;

    public ModelName? Model => (context.Declaration as KuroeNodeDeclaration)?.Model;

    public bool Stale => !_laidOut;

    /// <summary>该 run 的系统指令：节点声明块与本轮接口说明，置于请求最前。</summary>
    public string? SystemPrompt => context.SystemPrompt;

    public void LayOut(List<ChatMessage> messages)
    {
        _laidOut = true;

        messages.Clear();
        foreach (ContextMessage message in context.Seed)
        {
            messages.Add(new ChatMessage(Role(message.Role), message.Text));
        }
    }

    private static ChatRole Role(MessageRole role) => role switch
    {
        MessageRole.Assistant => ChatRole.Assistant,
        _ => ChatRole.User,
    };
}

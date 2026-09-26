using ApiHub.Shared.Models;
using Kuroe.Configuration;
using Kuroe.Shared.Agent;
using Microsoft.Extensions.AI;

namespace Kuroe.Agent.Sessions;

/// <summary>会话历史的来源：决定用哪个模型、历史何时重铺。</summary>
internal interface ISessionHistory
{
    /// <summary>本轮使用的模型，未选择时为空。</summary>
    string? Model { get; }

    /// <summary>当前历史是否已不适用。</summary>
    bool Stale { get; }

    /// <summary>重铺历史。</summary>
    void LayOut(List<ChatMessage> messages);
}

/// <summary>前台对话的历史：系统提示词取自当前设置，模型或提示词改动后旧上下文不再适用。</summary>
internal sealed class DialogueHistory(SettingsProvider settings) : ISessionHistory
{
    private AgentSettings? _bound;

    public string? Model => settings.Current.Agent.Model;

    public bool Stale => _bound is null || settings.Current.Agent.InvalidatesHistory(_bound);

    public void LayOut(List<ChatMessage> messages)
    {
        AgentSettings current = settings.Current.Agent;
        _bound = current;

        messages.Clear();
        messages.Add(new ChatMessage(ChatRole.System, current.SystemPrompt));
    }
}

/// <summary>agent 的历史：系统提示词之后按序铺入上游装配的内容，一次成型，执行期间不再改变来源。</summary>
internal sealed class RunHistory(SettingsProvider settings, RunContext context) : ISessionHistory
{
    private bool _laidOut;

    public string? Model => context.Model;

    public bool Stale => !_laidOut;

    public void LayOut(List<ChatMessage> messages)
    {
        _laidOut = true;

        messages.Clear();
        messages.Add(new ChatMessage(ChatRole.System, context.SystemPrompt ?? settings.Current.Agent.SystemPrompt));
        foreach (ContextMessage message in context.Seed)
        {
            messages.Add(new ChatMessage(Role(message.Role), message.Text));
        }
    }

    private static ChatRole Role(MessageRole role) => role switch
    {
        MessageRole.System => ChatRole.System,
        MessageRole.Assistant => ChatRole.Assistant,
        _ => ChatRole.User,
    };
}

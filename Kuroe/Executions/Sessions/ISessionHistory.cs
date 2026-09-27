using Kuroe.Configuration;
using Kuroe.Shared.Executions;
using Microsoft.Extensions.AI;

namespace Kuroe.Executions.Sessions;

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

/// <summary>前台对话的历史：模型改动后旧上下文不再适用，未写入过设置时按初次使用重铺。</summary>
internal sealed class DialogueHistory(SettingsProvider settings) : ISessionHistory
{
    private RuntimeSettings? _bound;

    public string? Model => settings.Current.Runtime.Model;

    public bool Stale => _bound is null || settings.Current.Runtime.InvalidatesHistory(_bound);

    public void LayOut(List<ChatMessage> messages)
    {
        _bound = settings.Current.Runtime;

        messages.Clear();
    }
}

/// <summary>一次执行的会话：只在来源丢失时重铺一次，执行期间来源不再改变。</summary>
internal sealed class RunHistory(RunContext context) : ISessionHistory
{
    private bool _laidOut;

    public string? Model => context.Model;

    public bool Stale => !_laidOut;

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

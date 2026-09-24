
using Kuroe.Shared.Agent;
using Kuroe.Shared.Agent.Turns;

namespace Kuroe.Agent.Turns;

/// <summary>一次模型请求的归属：哪个任务、哪个 agent、哪个步骤、哪个条目、记录写到哪里、过程写给谁。</summary>
public sealed record TurnScope
{
    /// <summary>所属任务。</summary>
    public required TaskId Task { get; init; }

    /// <summary>前台对话回合没有对应 agent。</summary>
    public required RunId? Run { get; init; }

    /// <summary>承担的角色，前台对话回合为空。</summary>
    public required RunRole? Role { get; init; }

    /// <summary>所属步骤名。</summary>
    public required string StepName { get; init; }

    /// <summary>所属条目序号，非按条目展开的步骤为空。</summary>
    public required int? ItemIndex { get; init; }

    /// <summary>本回合的过程记录。</summary>
    public required TurnJournal Journal { get; init; }

    /// <summary>本回合的过程写给谁。</summary>
    public required ITurnSink Sink { get; init; }
}

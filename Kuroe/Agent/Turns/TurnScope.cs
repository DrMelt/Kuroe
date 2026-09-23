
namespace Kuroe.Agent;

/// <summary>一次模型请求的归属：哪个任务、哪个 agent、哪个步骤、哪个条目、记录写到哪里、过程写给谁。</summary>
public sealed record TurnScope
{
    public required TaskId Task { get; init; }

    /// <summary>前台对话回合没有对应 agent。</summary>
    public required RunId? Run { get; init; }

    public required RunRole? Role { get; init; }

    public required string StepName { get; init; }

    public required int? ItemIndex { get; init; }

    public required TurnJournal Journal { get; init; }

    public required ITurnSink Sink { get; init; }
}

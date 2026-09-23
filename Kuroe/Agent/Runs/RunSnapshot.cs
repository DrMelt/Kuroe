using Kuroe.Agent.Turns;

namespace Kuroe.Agent.Runs;

/// <summary>一次 agent 执行的只读形状。</summary>
public sealed record RunSnapshot(
    RunId Id,
    RunContext Context,
    RunState State,
    string Progress,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? Result,
    IReadOnlyList<string> Failures,
    IReadOnlyList<JournalEntry> Journal,
    int DroppedEntries)
{
    public bool IsSettled => State.IsSettled();

    /// <summary>已耗时，未结束时按当前时刻算。</summary>
    public TimeSpan Elapsed => (FinishedAt ?? DateTimeOffset.UtcNow) - (StartedAt ?? CreatedAt);
}

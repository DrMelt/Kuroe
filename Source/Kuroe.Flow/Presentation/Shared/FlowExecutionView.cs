using Kuroe.Flow.Execution;

namespace Kuroe.Flow.Presentation;

/// <summary>展示用一次执行：标识、状态、过程与输出端口。</summary>
public sealed record FlowExecutionView(
    int Id,
    ExecutionState State,
    string? Progress,
    IReadOnlyList<FlowPortView> Outputs)
{
    /// <summary>该节点上的轮次，从 1 起。</summary>
    public int Round { get; init; }

    /// <summary>执行建立时刻。</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>执行启动时刻，未启动时为空。</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>执行收口时刻，未收口时为空。</summary>
    public DateTimeOffset? FinishedAt { get; init; }

    /// <summary>失败说明。</summary>
    public IReadOnlyList<string> Failures { get; init; } = [];

    /// <summary>是否留下恢复数据。</summary>
    public bool HasRestoreData { get; init; }

    /// <summary>继续自的执行号，不是恢复执行时为空。</summary>
    public int? ContinuedFrom { get; init; }

    /// <summary>已耗时，未收口时按当前时刻算。</summary>
    public TimeSpan Elapsed => ElapsedAt(DateTimeOffset.UtcNow);

    /// <summary>到给定时刻为止的已耗时，未收口时按该时刻算。</summary>
    public TimeSpan ElapsedAt(DateTimeOffset now) => (FinishedAt ?? now) - (StartedAt ?? CreatedAt);
}
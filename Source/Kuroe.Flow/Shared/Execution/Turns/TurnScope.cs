using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Shared.Execution.Turns;

/// <summary>一次模型请求的归属：哪个任务、哪个 run、哪个执行节点、哪个条目、记录写到哪里、过程写给谁。</summary>
public sealed record TurnScope
{
    /// <summary>所属任务。</summary>
    public required TaskId Task { get; init; }

    /// <summary>所属 run。</summary>
    public required RunId Run { get; init; }

    /// <summary>所属执行节点路径。</summary>
    public required NodePath Node { get; init; }

    /// <summary>所属条目序号，非按条目展开时为空。</summary>
    public required int? ItemIndex { get; init; }

    /// <summary>本回合的过程记录。</summary>
    public required ITurnJournal Journal { get; init; }

    /// <summary>本回合的过程写给谁。</summary>
    public required ITurnSink Sink { get; init; }
}

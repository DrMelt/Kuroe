using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Turns;
using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Executions.Turns;

/// <summary>一次模型请求的归属：哪个任务、哪个 run、哪个执行节点、哪个条目、记录写到哪里、过程写给谁。</summary>
public sealed record TurnScope
{
    /// <summary>所属任务。</summary>
    public required TaskId Task { get; init; }

    /// <summary>所属 run。</summary>
    public required RunId Run { get; init; }

    /// <summary>产出的契约。</summary>
    public required NodeOutput? Output { get; init; }

    /// <summary>所属执行节点名。</summary>
    public required NodeName NodeName { get; init; }

    /// <summary>所属条目序号，非按条目展开时为空。</summary>
    public required int? ItemIndex { get; init; }

    /// <summary>本回合的过程记录。</summary>
    public required TurnJournal Journal { get; init; }

    /// <summary>本回合的过程写给谁。</summary>
    public required ITurnSink Sink { get; init; }

    /// <summary>本轮可用的工具路径白名单，null 表示全部工具，空列表表示不放行任何工具。</summary>
    public IReadOnlyList<ToolPath>? Tools { get; init; }
}

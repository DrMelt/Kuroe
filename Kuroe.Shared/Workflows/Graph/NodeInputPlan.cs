using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Workflows.Graph;

/// <summary>节点实例空间的取得方式。</summary>
public enum SpaceKind
{
    /// <summary>整节点单实例，不按条目展开。</summary>
    None,

    /// <summary>取某执行节点交回的条目拆分。</summary>
    Split,

    /// <summary>取对齐来源已展开实例的并集。</summary>
    Aligned,
}

/// <summary>节点的启动驱动：什么条件下启动一个实例。</summary>
public enum NodeDriver
{
    /// <summary>来源有新版本即启动，整节点执行使用。</summary>
    Version,

    /// <summary>该实例尚无产出即启动，按条目展开使用。</summary>
    Completion,
}

/// <summary>输入侧的一条绑定：取值边从来源取值，空间边只提供实例空间来源。
/// 取值边按 <see cref="Scope"/> 取来源的哪一域，按 <see cref="Recurrence"/> 决定是否重复消费。</summary>
public sealed record InputBinding(
    EdgeKind Kind,
    int Source,
    FeedScope Scope,
    FeedRecurrence Recurrence,
    bool Signal);

/// <summary>输入侧的一个启动组：组内绑定对启动的实例全部可用时组齐备，组间任一齐备即可启动。
/// 组内有信号绑定时只认信号来源的新版本，否则认组内任一边的新版本。</summary>
public sealed record InputGroup(IReadOnlyList<InputBinding> Bindings)
{
    /// <summary>组是否由信号来源驱动。</summary>
    public bool DrivenBySignals => Bindings.Any(binding => binding.Signal);

    /// <summary>组内的信号绑定。</summary>
    public IEnumerable<InputBinding> Signals => Bindings.Where(binding => binding.Signal);
}

/// <summary>节点输入侧的编译期方案：实例空间的取得方式、启动组与驱动。
/// 方案在编译期由 <see cref="NodeGraph"/> 一次算出，判定只读它，不再反复扫图。</summary>
public sealed record NodeInputPlan
{
    /// <summary>实例空间的取得方式，整节点为 <see cref="SpaceKind.None"/>。</summary>
    public SpaceKind Space { get; init; }

    /// <summary>拆分来源序号，取得方式为 <see cref="SpaceKind.Split"/> 时给出。</summary>
    public int? SplitSource { get; init; }

    /// <summary>对齐来源序号，取得方式为 <see cref="SpaceKind.Aligned"/> 时给出，按入边序。</summary>
    public IReadOnlyList<int> AlignedSources { get; init; } = [];

    /// <summary>实例的分支过滤，未声明分支时为空。</summary>
    public BranchName? Branch { get; init; }

    /// <summary>启动组，按 From 的组序。</summary>
    public IReadOnlyList<InputGroup> Groups { get; init; } = [];

    /// <summary>启动驱动。</summary>
    public NodeDriver Driver { get; init; }

    /// <summary>单条实例的执行次数上限。</summary>
    public int MaxRuns { get; init; } = ExecutableNode.DefaultMaxRuns;
}

using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Graph;

namespace Kuroe.Workflows.Execution.Tasks;

/// <summary>运行期节点的只读状态，就绪判定按它取值。未实例化的节点给未放行、版本零、没有实例的默认状态。</summary>
internal interface ISourceState
{
    /// <summary>产出已放行，可被下游消费。</summary>
    bool Released { get; }

    /// <summary>整节点的产出版本，版本驱动的依据。</summary>
    long Revision { get; }

    /// <summary>该实例是否已有发表产出。</summary>
    bool ProducedItem(int item);
}

/// <summary>判定读的来源侧：节点状态与已交回的拆分。</summary>
internal interface IExecutionSources
{
    /// <summary>序号对应的节点状态，未实例化的节点给未放行的默认状态。</summary>
    ISourceState Source(int nodeIndex);

    /// <summary>某执行节点是否已交回拆分支票。</summary>
    bool HasSplit(int nodeIndex);

    /// <summary>某执行节点交回的条目序，按分支过滤；尚未交回为空。</summary>
    IReadOnlyList<int>? SplitItems(int nodeIndex, BranchName? branch);

    /// <summary>某执行节点已展开的实例集，尚未展开与整节点、容器为空。</summary>
    IReadOnlyList<int>? Items(int nodeIndex);
}

/// <summary>判定要读的目标自身状态：实例集、消费账、产出记录与执行次数。</summary>
internal interface IInputProgress
{
    /// <summary>已固定的实例集，尚未展开为空。</summary>
    IReadOnlyList<int>? AdoptedItems { get; }

    /// <summary>某来源的已消费版本，未消费过为零。</summary>
    long ConsumedRevision(int source);

    /// <summary>某来源的一次性消费是否已发生。</summary>
    bool ConsumedOnce(int source);

    /// <summary>整节点或某实例是否已有发表产出。</summary>
    bool Produced(int? item);

    /// <summary>整节点或某实例已启动的 run 数。</summary>
    int Runs(int? item);

    /// <summary>返工作废后要求越过就绪条件重启一次。</summary>
    bool ForceRerun { get; }
}

/// <summary>输入侧的就绪判定：实例空间、组齐备与驱动三个维度各一处实现，纯函数不改状态。
/// 组间任一齐备即启动，组内全部齐备；整节点驱动只有一个实例，按条目驱动按空间逐实例判定。</summary>
internal static class NodeReadiness
{
    /// <summary>实例集：已固定的实例集优先，否则按方案从拆分或对齐来源求出，来源未就绪时为空。</summary>
    public static IReadOnlyList<int> Instances(NodeInputPlan plan, IInputProgress progress, IExecutionSources sources)
    {
        if (progress.AdoptedItems is { } adopted)
        {
            return adopted;
        }

        return plan.Space switch
        {
            SpaceKind.Split => plan.SplitSource is { } split ? sources.SplitItems(split, plan.Branch) ?? [] : [],
            SpaceKind.Aligned => Aligned(plan, sources),
            _ => [],
        };
    }

    /// <summary>组齐备：任一组内的绑定对该实例全部可用。</summary>
    public static bool Available(NodeInputPlan plan, int? item, IExecutionSources sources) =>
        plan.Groups.Any(group => group.Bindings.All(binding => BindingAvailable(binding, item, sources)));

    /// <summary>可启动的实例：空间内的实例逐个按驱动与齐备判定，达到次数上限的不在其中。</summary>
    public static IReadOnlyList<int?> Ready(NodeInputPlan plan, IInputProgress progress, IExecutionSources sources)
    {
        List<int?> ready = [];
        foreach (int? item in Candidates(plan, progress, sources))
        {
            bool start = progress.Runs(item) < plan.MaxRuns && plan.Driver switch
            {
                NodeDriver.Version => progress.ForceRerun || Driven(plan, progress, sources),
                NodeDriver.Completion => !progress.Produced(item) && Available(plan, item, sources),
                _ => false,
            };
            if (start)
            {
                ready.Add(item);
            }
        }

        return ready;
    }

    /// <summary>版本驱动：任一组齐备且该组的驱动来源有新版本，组内有信号绑定时只认信号来源。</summary>
    private static bool Driven(NodeInputPlan plan, IInputProgress progress, IExecutionSources sources) =>
        plan.Groups.Any(group =>
            group.Bindings.All(binding => BindingAvailable(binding, null, sources))
            && (group.DrivenBySignals
                ? group.Signals.Any(binding => Fresh(binding, progress, sources))
                : group.Bindings.Any(binding => Fresh(binding, progress, sources))));

    /// <summary>候选实例：整节点驱动给单实例，按条目驱动给空间内的条目。</summary>
    private static IEnumerable<int?> Candidates(NodeInputPlan plan, IInputProgress progress, IExecutionSources sources) =>
        plan.Driver == NodeDriver.Completion ? Instances(plan, progress, sources).Select(item => (int?)item) : [null];

    /// <summary>一条绑定对该实例是否可用：空间边看来源是否已交回拆分，取值边按取值域取来源的产出。</summary>
    private static bool BindingAvailable(InputBinding binding, int? item, IExecutionSources sources)
    {
        if (binding.Kind == EdgeKind.Split)
        {
            return sources.HasSplit(binding.Source);
        }

        return binding.Scope switch
        {
            FeedScope.Whole or FeedScope.AllInstances => sources.Source(binding.Source).Released,
            FeedScope.Item when item is { } index => ItemAvailable(binding.Source, index, sources),
            _ => false,
        };
    }

    /// <summary>同序号取值对该条目是否可用：来源已展开实例集，且来源不负责该条目或该条目已产出。
    /// 来源不负责的条目视为齐备，来源负责的条目必须等它产出。</summary>
    private static bool ItemAvailable(int source, int item, IExecutionSources sources) =>
        sources.Items(source) is { } items
        && (!items.Contains(item) || sources.Source(source).ProducedItem(item));

    /// <summary>一条取值绑定相对消费账是否有新版本。空间边不驱动版本。</summary>
    private static bool Fresh(InputBinding binding, IInputProgress progress, IExecutionSources sources)
    {
        if (binding.Kind == EdgeKind.Split)
        {
            return false;
        }

        ISourceState source = sources.Source(binding.Source);
        return binding.Recurrence == FeedRecurrence.Once
            ? source.Released && !progress.ConsumedOnce(binding.Source)
            : source.Released && source.Revision > progress.ConsumedRevision(binding.Source);
    }

    /// <summary>对齐来源已展开实例的并集，按入边序去重，尚未展开的来源不贡献实例。</summary>
    private static List<int> Aligned(NodeInputPlan plan, IExecutionSources sources) =>
        [.. plan.AlignedSources.SelectMany(source => sources.Items(source) ?? []).Distinct()];
}

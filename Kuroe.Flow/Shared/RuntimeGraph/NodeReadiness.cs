using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Shared.RuntimeGraph;

/// <summary>输入侧的就绪判定：实例空间、组齐备与驱动三个维度各一处实现，纯函数不改状态。
/// 组间任一齐备即启动，组内全部齐备；整节点驱动只有一个实例，按条目驱动按空间逐实例判定。</summary>
public static class NodeReadiness
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
        AvailableGroup(plan, item, sources) is not null;

    /// <summary>第一个齐备的组序号，没有齐备的组时为空。</summary>
    public static int? AvailableGroup(NodeInputPlan plan, int? item, IExecutionSources sources)
    {
        for (int index = 0; index < plan.Groups.Count; index++)
        {
            if (plan.Groups[index].Bindings.All(binding => BindingAvailable(binding, item, sources)))
            {
                return index;
            }
        }

        return null;
    }

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

using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Shared.RuntimeGraph;

/// <summary>输入侧的就绪判定：组齐备与驱动两处实现，纯函数不改状态。
/// 组间任一齐备即启动，组内全部齐备；达到执行次数上限后不再启动。</summary>
public static class NodeReadiness
{
    /// <summary>组齐备：任一组内的绑定全部可用。</summary>
    public static bool Available(NodeInputPlan plan, IReadinessSources sources) =>
        AvailableGroup(plan, sources) is not null;

    /// <summary>第一个齐备的组序号，没有齐备的组时为空。</summary>
    public static int? AvailableGroup(NodeInputPlan plan, IReadinessSources sources)
    {
        for (int index = 0; index < plan.Groups.Count; index++)
        {
            if (plan.Groups[index].Bindings.All(binding => sources.Source(binding.Source).Released))
            {
                return index;
            }
        }

        return null;
    }

    /// <summary>是否可启动：执行次数未达上限，且有重跑意图或存在驱动来源有新版本的齐备组。</summary>
    public static bool Ready(NodeInputPlan plan, IInputProgress progress, IReadinessSources sources) =>
        progress.Executions < plan.MaxExecutions
        && (progress.ForceRerun || DrivenGroup(plan, progress, sources) is not null);

    /// <summary>第一个由来源新版本驱动的齐备组序号，没有这样的组时为空。</summary>
    public static int? DrivenGroup(NodeInputPlan plan, IInputProgress progress, IReadinessSources sources)
    {
        for (int index = 0; index < plan.Groups.Count; index++)
        {
            if (Driven(plan.Groups[index], progress, sources))
            {
                return index;
            }
        }

        return null;
    }

    /// <summary>组是否齐备且有驱动来源的新版本，组内有信号绑定时只认信号来源。</summary>
    private static bool Driven(InputGroup group, IInputProgress progress, IReadinessSources sources) =>
        group.Bindings.All(binding => sources.Source(binding.Source).Released)
        && (group.DrivenBySignals
            ? group.Signals.Any(binding => Fresh(binding, progress, sources))
            : group.Bindings.Any(binding => Fresh(binding, progress, sources)));

    /// <summary>一条绑定相对消费账是否有新版本。</summary>
    private static bool Fresh(InputBinding binding, IInputProgress progress, IReadinessSources sources)
    {
        ISourceState source = sources.Source(binding.Source);

        return source.Released && source.Revision > progress.ConsumedRevision(binding.Source);
    }
}

using Kuroe.Flow.Shared.RuntimeGraph;

namespace Kuroe.Flow.Shared.StaticGraph.Graph;

/// <summary>输入侧的就绪判定：组齐备与驱动两处实现，纯函数不改状态。
/// 组间任一齐备即启动，组内全部齐备；达到执行次数上限后不再启动。Trigger 绑定为 false 的组不驱动。</summary>
public sealed partial record NodeInputPlan
{
    /// <summary>组齐备：任一组内的绑定全部可用。</summary>
    public bool Available(IReadinessSources sources) => AvailableGroup(sources) is not null;

    /// <summary>第一个齐备的组序号，没有齐备的组时为空。</summary>
    public int? AvailableGroup(IReadinessSources sources)
    {
        for (int index = 0; index < Groups.Count; index++)
        {
            if (Groups[index].Bindings.All(binding => sources.Source(binding.Source).Released))
            {
                return index;
            }
        }

        return null;
    }

    /// <summary>是否可启动：执行次数未达上限，且有重跑意图或存在驱动来源有新版本的齐备组。</summary>
    public bool Ready(IInputProgress progress, IReadinessSources sources) =>
        progress.Executions < MaxExecutions
        && (progress.ForceRerun || DrivenGroup(progress, sources) is not null);

    /// <summary>第一个由来源新版本驱动且未被 Trigger 抑制的齐备组序号，没有这样的组时为空。</summary>
    public int? DrivenGroup(IInputProgress progress, IReadinessSources sources)
    {
        for (int index = 0; index < Groups.Count; index++)
        {
            if (DrivesNewVersion(Groups[index], progress, sources) && !Groups[index].Suppressed(sources))
            {
                return index;
            }
        }

        return null;
    }

    /// <summary>第一个有驱动来源新版本但存在触发值为 false 的齐备组，没有这样的组时为空。</summary>
    public InputGroup? SuppressedGroup(IInputProgress progress, IReadinessSources sources) =>
        Groups.FirstOrDefault(group => DrivesNewVersion(group, progress, sources) && group.Suppressed(sources));

    /// <summary>组是否齐备且有驱动来源的新版本，组内有信号绑定时只认信号来源。</summary>
    private static bool DrivesNewVersion(InputGroup group, IInputProgress progress, IReadinessSources sources) =>
        group.Bindings.All(binding => sources.Source(binding.Source).Released)
        && (group.DrivenBySignals
            ? group.Signals.Any(binding => Fresh(binding, progress, sources))
            : group.Bindings.Any(binding => Fresh(binding, progress, sources)));

    /// <summary>一条绑定相对消费账是否有新版本。</summary>
    private static bool Fresh(InputBinding binding, IInputProgress progress, IReadinessSources sources)
    {
        ISourceState source = sources.Source(binding.Source);

        return source.Released && source.Revision > progress.ConsumedRevision(binding.Key);
    }
}
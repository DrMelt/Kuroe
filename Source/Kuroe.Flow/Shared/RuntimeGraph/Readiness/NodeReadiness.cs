using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;

namespace Kuroe.Flow.Shared.RuntimeGraph.Readiness;

/// <summary>输入侧的就绪判定：纯函数读取方案与运行期状态，不改状态。
/// 组间任一齐备即启动，组内全部齐备；静态输入绑定的运行期常量全部发布后才进入启动判定；
/// Once 节点发布后不再启动；达到执行次数上限后不再启动。Trigger 绑定为 false 的组不驱动。</summary>
public static class NodeReadiness
{
    /// <summary>这一绑定是否触发下游。普通端口与节点级绑定恒触发。</summary>
    public static bool Triggers(this InputBinding binding, ISourceState source) =>
        !binding.CarriesTrigger || binding.Port is not { } port || source.Triggers(port);

    /// <summary>组内是否存在触发值为 false 的 Trigger 绑定。</summary>
    public static bool Suppressed(this InputGroup group, IReadinessSources sources) =>
        group.Bindings.Any(binding => !binding.Triggers(sources.Source(binding.Source)));

    /// <summary>组齐备：任一组内的绑定全部可用。</summary>
    public static bool Available(this NodeInputPlan plan, IReadinessSources sources) =>
        plan.AvailableGroup(sources) is not null;

    /// <summary>第一个齐备的组序号，没有齐备的组时为空。</summary>
    public static int? AvailableGroup(this NodeInputPlan plan, IReadinessSources sources)
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

    /// <summary>是否可启动：静态依赖全部发布、Once 节点尚未发布、执行次数未达上限，
    /// 且有重跑意图、静态输入启动条件或存在驱动来源有新版本的齐备组。</summary>
    public static bool Ready(this NodeInputPlan plan, IInputProgress progress, IReadinessSources sources) =>
        plan.StaticDependencies.All(dependency => sources.Source(dependency.Source).Released)
        && (plan.Emission != NodeEmission.Once || !progress.Published)
        && progress.Executions < plan.MaxExecutions
        && (progress.ForceRerun
            || plan.StaticOnly && progress.Executions == 0
            || plan.DrivenGroup(progress, sources) is not null);

    /// <summary>第一个由来源新版本驱动且未被 Trigger 抑制的齐备组序号，没有这样的组时为空。</summary>
    public static int? DrivenGroup(this NodeInputPlan plan, IInputProgress progress, IReadinessSources sources)
    {
        for (int index = 0; index < plan.Groups.Count; index++)
        {
            if (DrivesNewVersion(plan.Groups[index], progress, sources) && !plan.Groups[index].Suppressed(sources))
            {
                return index;
            }
        }

        return null;
    }

    /// <summary>第一个有驱动来源新版本但存在触发值为 false 的齐备组，没有这样的组时为空。</summary>
    public static InputGroup? SuppressedGroup(
        this NodeInputPlan plan,
        IInputProgress progress,
        IReadinessSources sources) =>
        plan.Groups.FirstOrDefault(group => DrivesNewVersion(group, progress, sources) && group.Suppressed(sources));

    /// <summary>组是否齐备且有驱动来源的新版本，组内有信号绑定时只认信号来源。</summary>
    private static bool DrivesNewVersion(InputGroup group, IInputProgress progress, IReadinessSources sources) =>
        group.Bindings.All(binding => sources.Source(binding.Source).Released)
        && (group.DrivenBySignals
            ? group.Signals.Any(binding => Fresh(binding, progress, sources))
            : group.Bindings.Any(binding => Fresh(binding, progress, sources)));

    /// <summary>一条绑定相对已消费版本是否有新版本。</summary>
    private static bool Fresh(InputBinding binding, IInputProgress progress, IReadinessSources sources)
    {
        ISourceState source = sources.Source(binding.Source);

        return source.Released && source.Revision > progress.ConsumedRevision(binding.Key);
    }
}

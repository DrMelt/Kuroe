using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Shared.RuntimeGraph;

/// <summary>判定读的来源侧：节点状态。节点按路径定位。</summary>
public interface IReadinessSources
{
    /// <summary>节点路径对应的节点状态，未实例化的节点给未放行的默认状态。</summary>
    ISourceState Source(NodePath node);
}

/// <summary>来源节点在某个输出端口上的最近一次产出是否触发下游。普通端口恒为 true，没有触发值的来源按 true 报告。</summary>
public interface IPortTrigger
{
    /// <summary>该端口最近一次产出是否触发下游。</summary>
    bool Triggers(PortName port) => true;
}

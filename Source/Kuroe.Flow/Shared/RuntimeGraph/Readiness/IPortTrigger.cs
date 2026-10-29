using Kuroe.Flow.Shared.Model;

namespace Kuroe.Flow.Shared.RuntimeGraph.Readiness;

/// <summary>来源节点在某个输出端口上的最近一次产出是否触发下游。普通端口恒为 true，没有触发值的来源按 true 报告。</summary>
public interface IPortTrigger
{
    /// <summary>该端口最近一次产出是否触发下游。</summary>
    bool Triggers(PortName port) => true;
}

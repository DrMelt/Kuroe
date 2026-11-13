using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Flow.Node.Shared.RuntimeGraph.Nodes;

/// <summary>一次发布的可观察状态：版本、各输出端口的触发位与内容同属一份产出。
/// 节点在发布点构造，读取方只按端口取值，不再回读节点当前状态。</summary>
public sealed record Publication(
    long Revision,
    IReadOnlyDictionary<PortName, bool> Triggers,
    IReadOnlyDictionary<PortName, IReadOnlyList<ContextValue>> Content)
{
    /// <summary>该端口在本次发布中是否触发下游，未给出的端口按触发处理。</summary>
    public bool Fires(PortName port) => Triggers.GetValueOrDefault(port, true);

    /// <summary>该端口在本次发布中的产出，没有内容时为空。</summary>
    public IReadOnlyList<ContextValue> Output(PortName port) => Content.GetValueOrDefault(port, []);

    /// <summary>该端口在本次发布中的单段文本，多条内容按出处顺序拼合。</summary>
    public string? Text(PortName port) => Output(port) is { Count: > 0 } values ? ContextValues.Join(values) : null;
}

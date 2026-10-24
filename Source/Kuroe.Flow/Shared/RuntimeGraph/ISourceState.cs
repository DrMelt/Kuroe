namespace Kuroe.Flow.Shared.RuntimeGraph;

/// <summary>运行期节点的只读状态，就绪判定按它取值。未实例化的节点给未放行、版本零的默认状态。</summary>
public interface ISourceState : IPortTrigger
{
    /// <summary>产出已放行，可被下游消费。</summary>
    bool Released { get; }

    /// <summary>产出版本，版本驱动的依据。</summary>
    long Revision { get; }
}

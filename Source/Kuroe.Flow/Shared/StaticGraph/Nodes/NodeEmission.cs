namespace Kuroe.Flow.Shared.StaticGraph.Nodes;

/// <summary>节点产出模式：逐次发布或首次发布后冻结。</summary>
public enum NodeEmission
{
    /// <summary>每次执行都发布一次产出，后续执行覆盖前一次。</summary>
    Repeated,

    /// <summary>首次成功发布后冻结，不再执行；未发布时可以失败重试与返工。</summary>
    Once,
}

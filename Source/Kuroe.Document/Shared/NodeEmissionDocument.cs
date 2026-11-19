namespace Kuroe.Document.Shared;

/// <summary>节点产出模式的描述取值：逐次发布或首次发布后冻结。未写时按逐次发布。</summary>
public enum NodeEmissionDocument
{
    /// <summary>每次执行都发布一次产出，后续执行覆盖前一次。</summary>
    Repeated,

    /// <summary>首次成功发布后冻结，不再执行。</summary>
    Once,
}
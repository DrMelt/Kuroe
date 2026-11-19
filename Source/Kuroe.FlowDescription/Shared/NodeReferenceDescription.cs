namespace Kuroe.FlowDescription.Shared;

/// <summary>引用节点：使用节点库定义，只写标签与使用处可覆盖的字段。</summary>
public sealed record NodeReferenceDescription : NodeDescription
{
    /// <summary>节点在图里的标识段。</summary>
    public required string Label { get; init; }

    /// <summary>引用的节点库定义名。</summary>
    public required string Use { get; init; }
}
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;

namespace Kuroe.Flow.TestSupport;

/// <summary>测试用的节点类型：给定类型名与静态形状，不实现校验契约。</summary>
public sealed class TestNodeType(NodeKind kind, NodeTypeShape shape) : INodeType
{
    /// <summary>类型名。</summary>
    public NodeKind Kind { get; } = kind;

    /// <summary>静态形状。</summary>
    public NodeTypeShape Shape { get; } = shape;
}

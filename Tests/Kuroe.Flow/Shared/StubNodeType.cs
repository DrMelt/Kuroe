using ErrorOr;
using Kuroe.Flow.Shared.StaticGraph.Node;

namespace Kuroe.Flow.Shared.Tests;

/// <summary>测试用的节点类型：类型名与静态形状，形状未给出时取默认。</summary>
internal sealed class StubNodeType(NodeKind kind, NodeTypeShape? shape = null) : INodeType
{
    /// <summary>类型名。</summary>
    public NodeKind Kind { get; } = kind;

    /// <summary>静态形状。</summary>
    public NodeTypeShape Shape { get; } = shape ?? new();

    /// <summary>类型规则，替身没有约束。</summary>
    public void Validate(NodeSpec node, NodeScope scope, Action<Error> fail)
    {
    }
}
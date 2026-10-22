using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph.Node;

namespace Kuroe.Console.Tests.Support;

/// <summary>测试用的节点类型：只有类型名，形状取默认。</summary>
internal sealed class StubNodeType(NodeKind kind) : INodeType
{
    /// <summary>类型名。</summary>
    public NodeKind Kind { get; } = kind;

    /// <summary>静态形状，取默认。</summary>
    public NodeTypeShape Shape { get; } = new();

    /// <summary>类型规则，替身没有约束。</summary>
    public void Validate(NodeSpec node, NodeScope scope, Action<Error> fail)
    {
    }
}
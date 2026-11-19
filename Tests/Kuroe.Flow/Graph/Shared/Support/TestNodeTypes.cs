using Kuroe.Flow.Graph.Shared.StaticGraph.Validation;
using Kuroe.Flow.Graph.Shared.Model;
using Kuroe.Flow.Graph.Shared.StaticGraph.Nodes;
using Kuroe.Executor.Shared.Ports;

namespace Kuroe.Flow.Graph.Shared.Tests.Support;

/// <summary>测试用的节点类型：给定类型名与静态形状，校验契约不做额外检查。</summary>
public sealed class TestNodeType(NodeKind kind, NodeTypeShape shape) : INodeType, INodeTypeValidation
{
    public NodeKind Kind { get; } = kind;

    public NodeTypeShape Shape { get; } = shape;

    public void Validate(NodeSpec node, NodeScope scope, ValidationErrorHandler fail)
    {
    }
}

/// <summary>测试用的节点类型实例。</summary>
public static class TestNodeTypes
{
    public static NodeKind ExecutableKind { get; } = new("Executable");

    public static INodeType Executable { get; } = new TestNodeType(ExecutableKind, new NodeTypeShape
    {
        DispatchesExecutions = true,
        AcceptsInferredInputs = true,
        InputTypes = [PortTypes.Text, PortTypes.Context, PortTypes.Signal],
        OutputTypes = [PortTypes.Text, PortTypes.Context, PortTypes.Signal],
    });

    public static INodeType PlainExecutable { get; } = Executable;
}

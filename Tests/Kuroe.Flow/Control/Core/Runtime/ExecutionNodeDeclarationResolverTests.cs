using Kuroe.Flow.Graph.Shared.StaticGraph;
using ErrorOr;
using Kuroe.Flow.Control.Runtime.Engine;
using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Ports;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.Tasks.Shared;
using Xunit;

namespace Kuroe.Flow.Control.Tests.Runtime;

/// <summary>执行节点声明解析：没有模型端口的节点不进入模型解析器。</summary>
public sealed class ExecutionNodeDeclarationResolverTests
{
    [Fact(DisplayName = "无模型节点返回空声明")]
    public void Unmodeled_node_returns_an_empty_declaration()
    {
        ExecutableNode node = new()
        {
            Path = NodePath.Of("节点"),
            Type = new UnmodeledType(),
            Spec = new ExecutableSpec(),
            Inputs = [],
            Outputs = [],
            MaxExecutions = ExecutableSpec.DefaultMaxExecutions,
        };
        ExecutionNodeDeclarationResolver resolver = new(new ThrowingResolver());

        ErrorOr<INodeDeclaration> result = resolver.For(node, PortTexts.Empty);

        Assert.False(result.IsError);
        Assert.Null(result.Value.Model);
    }

    private sealed class UnmodeledType : INodeType
    {
        public NodeKind Kind => new("Unmodeled");

        public NodeTypeShape Shape { get; } = new() { DispatchesExecutions = true };
    }

    private sealed class ThrowingResolver : IExecutionNodeDeclarationResolver
    {
        public ErrorOr<INodeDeclaration> For(ExecutableNode executable, IPortTexts portTexts) =>
            throw new InvalidOperationException("无模型端口的节点不应进入模型解析。");
    }
}
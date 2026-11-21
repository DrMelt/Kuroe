using ErrorOr;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Execution;
using ExecutableNode = Kuroe.Flow.Graph.Shared.StaticGraph.ExecutableNode;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Flow.Control.Runtime.Engine;

/// <summary>执行节点声明解析：没有模型端口的执行节点直接给出空声明，其余交给宿主解析器。</summary>
public sealed class ExecutionNodeDeclarationResolver(IExecutionNodeDeclarationResolver resolver) : IExecutionNodeDeclarationResolver
{
    /// <summary>取该执行节点的宿主声明。无模型端口的类型不需要模型声明。</summary>
    public ErrorOr<INodeDeclaration> For(ExecutableNode executable, IPortTexts portTexts) =>
        executable.Type.ModelPort is null
            ? new UnmodeledDeclaration()
            : resolver.For(executable, portTexts);

    /// <summary>没有模型端口的执行节点的空声明。</summary>
    private sealed record UnmodeledDeclaration : INodeDeclaration;
}
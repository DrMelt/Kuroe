using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Flow.Runtime.Engine;

/// <summary>执行目标解析：没有模型端口的执行节点直接给出空声明，其余交给宿主解析器。</summary>
public sealed class ExecutionTargetResolver(IExecutionTargetResolver resolver) : IExecutionTargetResolver
{
    /// <summary>取该执行节点的宿主声明。无模型端口的类型不需要模型声明。</summary>
    public ErrorOr<INodeDeclaration> For(ExecutableNode executable, IPortTexts portTexts) =>
        executable.Type.ModelPort is null
            ? new UnmodeledDeclaration()
            : resolver.For(executable, portTexts);

    /// <summary>不经过模型的执行节点使用的空声明。</summary>
    private sealed record UnmodeledDeclaration : INodeDeclaration;
}
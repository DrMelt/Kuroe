using ErrorOr;
using ExecutableNode = Kuroe.Flow.Graph.Shared.StaticGraph.ExecutableNode;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Flow.Graph.Shared.RuntimeGraph.Execution;

/// <summary>执行节点到宿主声明的解析。实现由宿主提供。</summary>
public interface IExecutionNodeDeclarationResolver
{
    /// <summary>取该执行节点的宿主声明，静态端口值优先于其它来源，无法确定时返回错误。</summary>
    ErrorOr<INodeDeclaration> For(
        ExecutableNode executable,
        IPortTexts portTexts);
}

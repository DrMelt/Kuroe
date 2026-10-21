using ErrorOr;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Flow.Shared.Execution;

/// <summary>执行节点到宿主声明的解析。由宿主实现，引擎只经它取得声明并交给执行器。</summary>
public interface IExecutionTargetResolver
{
    /// <summary>取该执行节点的宿主声明，无法确定时返回错误。</summary>
    ErrorOr<INodeDeclaration> For(ExecutableNode executable);
}

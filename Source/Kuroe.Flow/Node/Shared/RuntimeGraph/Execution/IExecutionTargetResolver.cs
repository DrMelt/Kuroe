using ErrorOr;
using ExecutableNode = Kuroe.Flow.Node.Shared.StaticGraph.Graph.ExecutableNode;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;

/// <summary>执行节点到宿主声明的解析。由宿主实现，引擎只经它取得声明并交给执行器。</summary>
public interface IExecutionTargetResolver
{
    /// <summary>取该执行节点的宿主声明，无法确定时返回错误。端口文本由引擎装配后传入，静态端口值优先。</summary>
    ErrorOr<INodeDeclaration> For(
        ExecutableNode executable,
        IPortTexts portTexts);
}

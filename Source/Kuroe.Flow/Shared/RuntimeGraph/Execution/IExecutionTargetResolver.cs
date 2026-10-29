using ErrorOr;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;

namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>执行节点到宿主声明的解析。由宿主实现，引擎只经它取得声明并交给执行器。</summary>
public interface IExecutionTargetResolver
{
    /// <summary>取该执行节点的宿主声明，无法确定时返回错误。静态端口文本由引擎解析后作为静态视图传入。</summary>
    ErrorOr<INodeDeclaration> For(
        ExecutableNode executable,
        IPortTexts staticTexts);
}

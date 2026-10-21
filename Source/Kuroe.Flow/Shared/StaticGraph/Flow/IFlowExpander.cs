using ErrorOr;
using Kuroe.Flow.Shared.StaticGraph.Node;

namespace Kuroe.Flow.Shared.StaticGraph.Flow;

/// <summary>库装配：把授权流程与节点库定义展开成具体流程。</summary>
public interface IFlowExpander
{
    /// <summary>把内置定义与文件定义合并，校验节点库后逐流程展开。错误一次给全。</summary>
    ErrorOr<IReadOnlyList<FlowDefinition>> Expand(
        IReadOnlyList<FlowDefinition> authoringFlows,
        IReadOnlyList<NodeSpec> builtins,
        IReadOnlyList<NodeSpec> fileNodes);
}
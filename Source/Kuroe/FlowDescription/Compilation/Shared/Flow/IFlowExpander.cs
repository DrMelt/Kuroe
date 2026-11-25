using ErrorOr;
using Kuroe.FlowDescription.Definition.Shared.Flow;
using Kuroe.FlowDescription.Definition.Shared.Nodes;

namespace Kuroe.FlowDescription.Compilation.Shared.Flow;

/// <summary>库装配：把授权流程与节点库定义展开成具体流程。</summary>
public interface IFlowExpander
{
    /// <summary>校验文件节点库后逐流程展开。错误一次给全。</summary>
    ErrorOr<IReadOnlyList<FlowDefinition>> Expand(
        IReadOnlyList<FlowDefinition> authoringFlows,
        IReadOnlyList<NodeSpec> fileNodes);
}
using ErrorOr;
using Kuroe.FlowDescription.Shared.StaticGraph.Flow;
using Kuroe.FlowDescription.Shared.StaticGraph.Nodes;

namespace Kuroe.FlowDescription.Shared.Compilation;

/// <summary>流程描述的统一编译入口：描述构造、库展开、模型校验与图编译。</summary>
public interface IFlowDescriptionCompiler
{
    /// <summary>编译一份流程描述。内置定义由调用方给出，文件定义来自描述。</summary>
    ErrorOr<IReadOnlyList<FlowGraph>> Compile(FlowDescriptionSet descriptionSet, IReadOnlyList<NodeSpec> builtins);
}
using ErrorOr;
using Kuroe.FlowDescription.Shared.StaticGraph.Flow;
using Kuroe.FlowDescription.Shared.StaticGraph.Nodes;

namespace Kuroe.FlowDescription.Shared.Compilation;

/// <summary>流程描述的统一编译入口：描述构造、库展开与语言校验。
/// 输出展开后的流程定义，不包含图静态模型。</summary>
public interface IFlowDescriptionCompiler
{
    /// <summary>编译一份流程描述。内置定义由调用方给出，文件定义来自描述。</summary>
    ErrorOr<IReadOnlyList<FlowDefinition>> Compile(
        FlowDescriptionSet descriptionSet,
        IReadOnlyList<NodeSpec> builtins);
}

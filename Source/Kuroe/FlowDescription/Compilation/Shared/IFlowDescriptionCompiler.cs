using ErrorOr;
using Kuroe.FlowDescription.Description.Shared;
using Kuroe.FlowDescription.Definition.Shared.Flow;

namespace Kuroe.FlowDescription.Compilation.Shared;

/// <summary>流程描述的统一编译入口：描述构造、库展开与语言校验。
/// 输出展开后的流程定义，不包含图静态模型。</summary>
public interface IFlowDescriptionCompiler
{
    /// <summary>编译一份流程描述，节点库来自描述。</summary>
    ErrorOr<IReadOnlyList<FlowDefinition>> Compile(FlowDescriptionSet descriptionSet);
}

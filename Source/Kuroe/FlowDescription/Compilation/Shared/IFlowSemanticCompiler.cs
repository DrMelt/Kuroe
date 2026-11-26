using ErrorOr;
using Kuroe.FlowDescription.Description.Shared;
using Kuroe.FlowDescription.Semantics.Shared;

namespace Kuroe.FlowDescription.Compilation.Shared;

/// <summary>流程描述的编译入口：给出语言合法且引用可解析的流程语义结果。</summary>
public interface IFlowSemanticCompiler
{
    /// <summary>编译一份流程描述，节点库来自描述。跨流程诊断一次聚合，单流程解析失败时不再执行语言规则。</summary>
    ErrorOr<IReadOnlyList<FlowSemantics>> CompileWithSemantics(FlowDescriptionSet descriptionSet);
}
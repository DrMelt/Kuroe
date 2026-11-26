using Kuroe.FlowDescription.Definition.Shared.Flow;

namespace Kuroe.FlowDescription.Semantics.Shared;

/// <summary>流程语言语义解释入口：把展开后的流程定义解析为来源终点与接线结果。</summary>
public interface IFlowSemanticsAnalyzer
{
    /// <summary>解析一条流程定义。诊断随结果给出，不抛出。</summary>
    FlowSemantics Analyze(FlowDefinition flow);
}
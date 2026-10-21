using Kuroe.Node.Approval;
using Kuroe.Node.Input;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Node.Llm;

namespace Kuroe.Presentation;

/// <summary>节点类型的宿主侧读法：把各类型的声明与类型映射成宿主需要的值，引擎不参与这一步。</summary>
public static class NodeParameterView
{
    /// <summary>Llm 类型的声明，节点不是该类型时为空。</summary>
    public static LlmParameters? Llm(INodeType? type) => (type as LlmNodeType)?.Parameters;

    /// <summary>等待型节点的提问文本，未声明或类型不符时为空。</summary>
    public static string? Question(INodeType? type) => type switch
    {
        InputNodeType { Parameters.Question: { } input } => input,
        ApprovalNodeType { Parameters.Question: { } approval } => approval,
        _ => null,
    };
}
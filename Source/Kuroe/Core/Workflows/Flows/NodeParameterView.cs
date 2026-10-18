using Kuroe.Node.Approval;
using Kuroe.Node.Input;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Node.Llm;
using Kuroe.Pending;

namespace Kuroe.Workflows.Flows;

/// <summary>节点类型的宿主侧读法：把各类型的声明与类型映射成宿主需要的值，引擎不参与这一步。</summary>
public static class NodeParameterView
{
    /// <summary>等待型节点的等待种类：审批节点等批准，其余按等回答处理。</summary>
    public static PendingKind PendingKindOf(INodeType? type) =>
        type is ApprovalNodeType ? PendingKind.Approve : PendingKind.Answer;

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
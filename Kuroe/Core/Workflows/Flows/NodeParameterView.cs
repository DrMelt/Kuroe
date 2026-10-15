using Kuroe.Flow.Node.Approval;
using Kuroe.Flow.Node.Input;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.Node.Llm;

namespace Kuroe.Workflows.Flows;

/// <summary>节点参数的展示读取：把各类型的参数映射成宿主展示需要的值，引擎不参与这一步。</summary>
public static class NodeParameterView
{
    /// <summary>Llm 类型的参数，节点不是该类型时为空。</summary>
    public static LlmParameters? Llm(INodeParameters? parameters) => parameters as LlmParameters;

    /// <summary>等待型节点的提问文本，未声明或类型不符时为空。</summary>
    public static string? Question(INodeParameters? parameters) => parameters switch
    {
        InputParameters input => input.Question,
        ApprovalParameters approval => approval.Question,
        _ => null,
    };
}
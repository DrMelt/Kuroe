namespace Kuroe.Shared.Workflows.Flows;

/// <summary>一棵流程树展平后的一片叶子及其解析结果。叶子是唯一会派发 agent 的节点。</summary>
public sealed record LeafNode(
    int Index,
    string Name,
    string Path,
    AgentDefinition Agent,
    string? Prompt,
    NodeOutput Output,
    NodeMode Mode,
    IReadOnlyList<int> From,
    NodeGate Gate,
    RejectAction? OnReject,
    int? MaxAttempts,
    SplitConfig? Split)
{
    /// <summary>检查未声明时按退回返工处理。</summary>
    public RejectAction RejectAction => OnReject ?? RejectAction.Retry;

    /// <summary>检查未声明时按两轮处理，值域由 WorkflowRules 校验保证。</summary>
    public int AttemptLimit => MaxAttempts ?? 2;

    /// <summary>拆分只有固定条目，模型不参与补充。</summary>
    public bool IsStaticSplit => Split is { Items.Count: > 0, ExtrasMax: null or 0 };
}
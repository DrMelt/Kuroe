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
    int? MaxAttempts)
{
    /// <summary>检查未声明时按退回返工处理。</summary>
    public RejectAction RejectAction => OnReject ?? RejectAction.Retry;

    /// <summary>检查未声明时按两轮处理。</summary>
    public int AttemptLimit => Math.Max(1, MaxAttempts ?? 2);
}
using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Workflows.Graph;

/// <summary>一棵流程树展平后的一个执行节点及其解析结果。执行节点是唯一会派发执行的节点。</summary>
public sealed record ExecutableNode(
    int Index,
    NodeName Name,
    string Path,
    NodeGate Gate,
    ModelDefinition Model,
    IReadOnlyList<ToolName> Tools,
    string? Prompt,
    NodeOutput Output,
    NodeMode Mode,
    BranchName? Branch,
    IReadOnlyList<int> From,
    RejectAction? OnReject,
    int? MaxAttempts,
    SplitConfig? Split) : GraphNode(Index, Name, Path, Gate)
{
    /// <summary>检查未声明时按退回返工处理。</summary>
    public RejectAction RejectAction => OnReject ?? RejectAction.Retry;

    /// <summary>检查未声明时按两轮处理，值域由 WorkflowRules 校验保证。</summary>
    public int AttemptLimit => MaxAttempts ?? 2;

    /// <summary>拆分只有固定条目，模型不参与补充。</summary>
    public bool IsStaticSplit => Split is { Items.Count: > 0, ExtrasMax: null or 0 };
}
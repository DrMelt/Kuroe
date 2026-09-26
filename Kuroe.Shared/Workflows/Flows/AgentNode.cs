namespace Kuroe.Shared.Workflows.Flows;

/// <summary>叶子节点：指派一个 agent 承担本节点的执行，是唯一真实执行的节点。</summary>
public sealed record AgentNode : NodeSpec
{
    /// <summary>引用的 agent 定义名。</summary>
    public required string Agent { get; init; }

    /// <summary>交回什么。</summary>
    public NodeOutput Output { get; init; } = NodeOutput.Plain;

    /// <summary>整叶一个 agent 还是按规划条目各派一个。</summary>
    public NodeMode Mode { get; init; } = NodeMode.Single;

    /// <summary>产出即开下一步还是停在待批准。</summary>
    public NodeGate Gate { get; init; } = NodeGate.Auto;

    /// <summary>检查不通过的处置，非 Review 节点不得声明。</summary>
    public RejectAction? OnReject { get; init; }

    /// <summary>允许的检查轮数，非 Review 节点不得声明。</summary>
    public int? MaxAttempts { get; init; }

    /// <summary>检查未声明时按退回返工处理。</summary>
    public RejectAction RejectAction => OnReject ?? RejectAction.Retry;

    /// <summary>检查未声明时按两轮处理，值域由校验保证。</summary>
    public int AttemptLimit => MaxAttempts ?? 2;

    /// <summary>拆分源的固定配置：静态条目与模型补充约束。</summary>
    public SplitConfig? Split { get; init; }

    /// <summary>拆分只有固定条目，模型不参与补充。</summary>
    public bool IsStaticSplit => Split is { Items.Count: > 0, ExtrasMax: null or 0 };
}
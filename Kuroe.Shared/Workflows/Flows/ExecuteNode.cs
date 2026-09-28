namespace Kuroe.Shared.Workflows.Flows;

/// <summary>执行节点：自带会话能力配置，是唯一真实执行的节点。</summary>
public sealed record ExecuteNode : NodeSpec
{
    /// <summary>引用的模型配置名。</summary>
    public required string Model { get; init; }

    /// <summary>能力工具白名单，按函数名匹配。未写或空时不给出任何能力工具。</summary>
    public IReadOnlyList<string> Tools { get; init; } = [];

    /// <summary>交回什么。</summary>
    public NodeOutput Output { get; init; } = NodeOutput.Plain;

    /// <summary>整节点一个执行还是按规划条目各派一个。</summary>
    public NodeMode Mode { get; init; } = NodeMode.Single;

    /// <summary>检查不通过的处置，非 Review 节点不得声明。</summary>
    public RejectAction? OnReject { get; init; }

    /// <summary>允许的检查轮数，非 Review 节点不得声明。</summary>
    public int? MaxAttempts { get; init; }

    /// <summary>检查未声明时按退回返工处理。</summary>
    public RejectAction RejectAction => OnReject ?? RejectAction.Retry;

    /// <summary>检查未声明时按两轮处理，值域由校验保证。</summary>
    public int AttemptLimit => MaxAttempts ?? 2;

    /// <summary>本执行节点只处理拆分中归属该分支的条目，未写时处理全部条目。</summary>
    public string? Branch { get; init; }

    /// <summary>拆分源的固定配置：静态条目与模型补充约束。</summary>
    public SplitConfig? Split { get; init; }

    /// <summary>拆分只有固定条目，模型不参与补充。</summary>
    public bool IsStaticSplit => Split is { Items.Count: > 0, ExtrasMax: null or 0 };
}
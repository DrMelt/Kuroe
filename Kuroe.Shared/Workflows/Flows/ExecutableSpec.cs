using Kuroe.Shared.Executions.Tools;

namespace Kuroe.Shared.Workflows.Flows;

/// <summary>执行配置：执行节点带有的能力声明。模型选择由节点自身的 Model 引用承载。</summary>
public sealed record ExecutableSpec
{
    /// <summary>能力工具白名单，按工具路径前缀匹配，写上级路径即放行整棵子树。未写或空时不给出任何能力工具。</summary>
    public IReadOnlyList<ToolPath> Tools { get; init; } = [];

    /// <summary>对执行单元的额外要求，与目标一起构成指令。</summary>
    public string? Prompt { get; init; }

    /// <summary>交回什么。</summary>
    public NodeOutput Output { get; init; } = NodeOutput.Text;

    /// <summary>整节点一个执行还是按规划条目各派一个。</summary>
    public NodeMode Mode { get; init; } = NodeMode.Single;

    /// <summary>本执行节点只处理拆分中归属该分支的条目，未写时处理全部条目。</summary>
    public BranchName? Branch { get; init; }

    /// <summary>拆分源的固定配置：静态条目与模型补充约束。</summary>
    public SplitConfig? Split { get; init; }

    /// <summary>可选启动条件组：From 组必须先齐备，再满足任一组成员齐备才启动。组内成员并取；组内与组间允许重复引用，任两组按成员顺序不得完全相同。</summary>
    public IReadOnlyList<IReadOnlyList<NodeName>> AnyOf { get; init; } = [];

    /// <summary>输出校验：收口时校验模型产出，不通过则节点阻塞待返工。</summary>
    public OutputValidation? Validate { get; init; }

    /// <summary>同一执行路径的最高执行次数，达到后不再启动新 run。未写时取默认值。</summary>
    public int? MaxRuns { get; init; }

    /// <summary>拆分只有固定条目，模型不参与补充。</summary>
    public bool IsStaticSplit => Split is { Items.Count: > 0, ExtrasMax: null or 0 };
}

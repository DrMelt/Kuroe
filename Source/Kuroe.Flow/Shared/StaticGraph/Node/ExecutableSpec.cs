namespace Kuroe.Flow.Shared.StaticGraph.Node;

/// <summary>执行配置：执行节点的产出校验与执行上限声明。能力与措辞等类型专属声明由节点类型与参数承载。</summary>
public sealed record ExecutableSpec
{
    /// <summary>输出校验：收口时校验执行产出，不通过则节点阻塞待返工。</summary>
    public OutputValidation? Validate { get; init; }

    /// <summary>同一执行路径的最高执行次数，达到后不再启动新执行。未写时取默认值。</summary>
    public int? MaxExecutions { get; init; }
}

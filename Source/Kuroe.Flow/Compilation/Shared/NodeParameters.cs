namespace Kuroe.Flow.Compilation.Shared;

/// <summary>节点类型参数段：描述文档里类型专属字段的唯一位置。键与取值的含义由节点类型解释，
/// 引擎只把它交给类型描述，不按字段名读取或诊断。</summary>
/// <param name="Values">字段名到字段值的映射，按原文保留字段名。</param>
public sealed record NodeParameters(IReadOnlyDictionary<string, FlowValue> Values)
{
    /// <summary>没有写参数段的节点取此值。</summary>
    public static NodeParameters Empty { get; } = new(new Dictionary<string, FlowValue>());
}
namespace Kuroe.Flow.Shared.Descriptions;

/// <summary>参数段的一个字段声明：字段名与取值形态。</summary>
/// <param name="Name">字段名，与描述里的键按原文匹配。</param>
/// <param name="Kind">取值形态。</param>
public sealed record ParameterField(string Name, ParameterValueKind Kind);
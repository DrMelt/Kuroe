namespace Kuroe.Flow.Json.Parameters;

/// <summary>参数段的一轮取值里的一个字段：名字、形态与内容，形态决定内容在哪一边。</summary>
public sealed record ParameterValue
{
    /// <summary>字段名。</summary>
    public required string Name { get; init; }

    /// <summary>取值形态。</summary>
    public required ParameterValueKind Kind { get; init; }

    /// <summary>文本值，形态为文本数组时为空。</summary>
    public string? Text { get; init; }

    /// <summary>文本数组，形态为文本时为空集合。</summary>
    public IReadOnlyList<string> Texts { get; init; } = [];
}
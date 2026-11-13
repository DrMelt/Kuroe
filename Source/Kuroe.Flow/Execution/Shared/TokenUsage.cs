namespace Kuroe.Flow.Execution.Shared;

/// <summary>一次执行累计的模型 token 用量。字段为 provider 给出的计数，未提供时为空。
/// 缓存输入是输入的子集，推理是输出的子集，都不参与合计。</summary>
public sealed record TokenUsage(
    long? Input,
    long? Output,
    long? Total,
    long? CachedInput,
    long? Reasoning)
{
    /// <summary>没有取得任何用量的形态。</summary>
    public static TokenUsage None { get; } = new(null, null, null, null, null);

    /// <summary>是否取得请求级计数。缓存与推理是输入与输出的子集，单独出现不构成用量。</summary>
    public bool Known => Input is not null || Output is not null || Total is not null;

    /// <summary>相加两个用量，合计按两侧各自可得的合计相加。</summary>
    public TokenUsage Add(TokenUsage other)
    {
        long? input = Sum(Input, other.Input);
        long? output = Sum(Output, other.Output);
        long? total = Sum(DerivedTotal, other.DerivedTotal);
        if (total is null && input is { } inputCount && output is { } outputCount)
        {
            total = inputCount + outputCount;
        }

        return new TokenUsage(
            input,
            output,
            total,
            Sum(CachedInput, other.CachedInput),
            Sum(Reasoning, other.Reasoning));
    }

    /// <summary>本用量的合计：输入与输出都已知时按两者相加，否则取给出的合计。</summary>
    private long? DerivedTotal => Input is { } input && Output is { } output ? input + output : Total;

    /// <summary>两侧都有值时相加，只有一侧有值时取该值。</summary>
    private static long? Sum(long? left, long? right) => (left, right) switch
    {
        ({ } leftCount, { } rightCount) => leftCount + rightCount,
        ({ } leftCount, null) => leftCount,
        (null, { } rightCount) => rightCount,
        _ => null,
    };
}
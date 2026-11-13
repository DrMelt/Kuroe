namespace Kuroe.Execution.Shared;

/// <summary>一次执行的模型请求用量：请求次数、未取得用量的请求数、token 合计与最近一次返回用量的请求的上下文 token 数。</summary>
public sealed record ExecutionUsage(int RequestCount, int RequestsWithoutUsage, TokenUsage Tokens)
{
    /// <summary>没有发起模型请求的形态。Tokens 为 <see cref="TokenUsage.None"/>，表示也没有取得请求级计数。</summary>
    public static ExecutionUsage None { get; } = new(0, 0, TokenUsage.None);

    /// <summary>最近一次返回用量的请求的输入 token 数；该请求没有给出输入 token 数，或其后有请求未返回用量时为空。该值不随请求累计。</summary>
    public long? ContextTokens { get; init; }

    /// <summary>记下一次已发起的请求，请求由此进入统计。</summary>
    public ExecutionUsage BeginRequest() => this with { RequestCount = RequestCount + 1 };

    /// <summary>收下一次请求的用量，没有取得时计入未提供并清空上下文。</summary>
    public ExecutionUsage CompleteRequest(TokenUsage? usage) => usage is { Known: true } known
        ? this with { Tokens = Tokens.Add(known), ContextTokens = known.Input }
        : this with { RequestsWithoutUsage = RequestsWithoutUsage + 1, ContextTokens = null };
}
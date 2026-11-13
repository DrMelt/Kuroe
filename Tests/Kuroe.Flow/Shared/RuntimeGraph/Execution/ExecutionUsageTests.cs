using Xunit;
using ExecutionUsage = Kuroe.Execution.Shared.ExecutionUsage;
using TokenUsage = Kuroe.Execution.Shared.TokenUsage;

namespace Kuroe.Flow.Shared.Tests.RuntimeGraph.Execution;

/// <summary>模型请求用量值对象：计数、缺失与合并规则。</summary>
public sealed class ExecutionUsageTests
{
    [Fact]
    public void A_request_without_usage_is_counted_as_missing()
    {
        ExecutionUsage usage = ExecutionUsage.None.BeginRequest().CompleteRequest(null);

        Assert.Equal(1, usage.RequestCount);
        Assert.Equal(1, usage.RequestsWithoutUsage);
        Assert.False(usage.Tokens.Known);
    }

    [Fact]
    public void Requests_add_usage_and_counts()
    {
        ExecutionUsage usage = ExecutionUsage.None
            .BeginRequest()
            .CompleteRequest(new TokenUsage(100, 20, 120, 80, 5))
            .BeginRequest()
            .CompleteRequest(new TokenUsage(50, 10, 60, 10, 1));

        Assert.Equal(2, usage.RequestCount);
        Assert.Equal(0, usage.RequestsWithoutUsage);
        Assert.Equal(150, usage.Tokens.Input);
        Assert.Equal(30, usage.Tokens.Output);
        Assert.Equal(180, usage.Tokens.Total);
        Assert.Equal(90, usage.Tokens.CachedInput);
        Assert.Equal(6, usage.Tokens.Reasoning);
    }

    [Fact]
    public void Context_follows_the_latest_request_without_being_summed()
    {
        ExecutionUsage usage = ExecutionUsage.None
            .BeginRequest()
            .CompleteRequest(new TokenUsage(100, 20, 120, 80, 5))
            .BeginRequest()
            .CompleteRequest(new TokenUsage(50, 10, 60, 10, 1));

        Assert.Equal(150, usage.Tokens.Input);
        Assert.Equal(50, usage.ContextTokens);
    }

    [Fact]
    public void A_request_without_usage_clears_the_context()
    {
        ExecutionUsage usage = ExecutionUsage.None
            .BeginRequest()
            .CompleteRequest(new TokenUsage(100, 20, 120, 80, 5))
            .BeginRequest()
            .CompleteRequest(null);

        Assert.Equal(100, usage.Tokens.Input);
        Assert.Null(usage.ContextTokens);
    }

    [Fact]
    public void A_request_without_input_leaves_the_context_empty()
    {
        ExecutionUsage usage = ExecutionUsage.None
            .BeginRequest()
            .CompleteRequest(new TokenUsage(null, 20, 20, null, null));

        Assert.Null(usage.ContextTokens);
    }

    [Fact]
    public void Unknown_usage_is_not_treated_as_zero()
    {
        TokenUsage usage = new TokenUsage(100, null, null, null, null)
            .Add(new TokenUsage(null, 20, null, null, null));

        Assert.Equal(100, usage.Input);
        Assert.Equal(20, usage.Output);
        Assert.Equal(120, usage.Total);
        Assert.Null(usage.CachedInput);
    }

    [Fact]
    public void Provider_total_is_kept_when_input_or_output_is_missing()
    {
        TokenUsage usage = new TokenUsage(null, null, 70, null, null)
            .Add(new TokenUsage(null, null, 30, null, null));

        Assert.Equal(100, usage.Total);
    }

    [Fact]
    public void Subset_counts_alone_are_not_usage()
    {
        TokenUsage usage = new(null, null, null, 80, 5);

        Assert.False(usage.Known);
    }

    [Fact]
    public void Totals_from_both_sides_are_kept()
    {
        TokenUsage usage = new TokenUsage(100, 20, null, null, null)
            .Add(new TokenUsage(null, null, 50, null, null));

        Assert.Equal(100, usage.Input);
        Assert.Equal(20, usage.Output);
        Assert.Equal(170, usage.Total);
    }

    [Fact]
    public void A_request_with_only_subset_counts_is_counted_as_missing()
    {
        ExecutionUsage usage = ExecutionUsage.None
            .BeginRequest()
            .CompleteRequest(new TokenUsage(null, null, null, 80, 5));

        Assert.Equal(1, usage.RequestsWithoutUsage);
        Assert.False(usage.Tokens.Known);
    }
}
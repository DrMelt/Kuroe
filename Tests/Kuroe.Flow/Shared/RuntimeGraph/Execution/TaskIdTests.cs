using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Xunit;

namespace Kuroe.Flow.Shared.Tests.RuntimeGraph.Execution;

/// <summary>任务键的生成、显示与引用匹配。</summary>
public sealed class TaskIdTests
{
    [Fact]
    public void New_produces_unique_full_keys()
    {
        TaskId[] ids = [.. Enumerable.Range(0, 1000).Select(_ => TaskId.New())];

        Assert.All(ids, id => Assert.True(TaskId.TryParse(id.Value, out _)));
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void Short_is_the_last_eight_characters()
    {
        TaskId id = new("0123456789abcdef0123456789abcdef");

        Assert.Equal("89abcdef", id.Short);
        Assert.Equal("任务 89abcdef", id.ToString());
    }

    [Fact]
    public void TryParse_accepts_only_a_full_hex_key()
    {
        Assert.True(TaskId.TryParse("0123456789abcdef0123456789abcdef", out TaskId parsed));
        Assert.Equal("0123456789abcdef0123456789abcdef", parsed.Value);
        Assert.False(TaskId.TryParse("1", out _));
        Assert.False(TaskId.TryParse("0123456789abcdef0123456789abcde", out _));
        Assert.False(TaskId.TryParse("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz", out _));
        Assert.False(TaskId.TryParse(null, out _));
    }

    [Fact]
    public void MatchesShort_compares_the_tail_ignoring_case()
    {
        TaskId id = new("0123456789abcdef0123456789ABCDEF");

        Assert.True(id.MatchesShort("89abcdef"));
        Assert.True(id.MatchesShort("89ABCDEF"));
        Assert.False(id.MatchesShort("01234567"));
        Assert.False(id.MatchesShort(string.Empty));
    }

    [Fact]
    public void Fixed_keys_use_a_full_width_hexadecimal_value()
    {
        TaskId id = new(3);

        Assert.Equal("00000000000000000000000000000003", id.Value);
        Assert.Equal("00000003", id.Short);
        Assert.Equal("任务 00000003", id.ToString());
    }
}

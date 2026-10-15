using Kuroe.Flow.RuntimeGraph;
using Xunit;

namespace Kuroe.Flow.Tests;

/// <summary>本轮结论：交付完毕与无法完成的区分。</summary>
public sealed class RunVerdictTests
{
    [Fact]
    public void Run_verdict_distinguishes_completion_from_a_blocked_run()
    {
        Assert.False(RunVerdict.Done.Blocked);
        Assert.Null(RunVerdict.Done.Reason);

        RunVerdict blocked = RunVerdict.CannotComplete("缺输入");

        Assert.True(blocked.Blocked);
        Assert.Equal("缺输入", blocked.Reason);
    }
}
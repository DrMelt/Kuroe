using Kuroe.Shared.Agent;
using Kuroe.Shared.Agent.Runs;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Tests;

/// <summary>过程与上下文的上限收口。</summary>
public sealed class LimitsTests
{
    [Fact]
    public void Title_is_trimmed_to_the_first_line()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        TaskSnapshot snapshot = harness.Snapshot(harness.Submit("第一行\n第二行"));

        Assert.Equal("第一行", snapshot.Title);
    }

    [Fact]
    public void Title_is_truncated_at_forty_characters()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        string goal = new string('目', 50);

        TaskSnapshot snapshot = harness.Snapshot(harness.Submit(goal));

        Assert.Equal(41, snapshot.Title.Length);
        Assert.StartsWith(new string('目', 40), snapshot.Title);
        Assert.EndsWith("…", snapshot.Title);
    }

    [Fact]
    public void Upstream_output_in_context_is_capped_with_an_ellipsis()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.Output = _ => new string('长', 3000);

        TaskSnapshot done = harness.Settle(harness.Submit("补齐 README"));
        RunSnapshot implement = Assert.Single(done.Steps[1].Runs, run => run.Context.ItemIndex == 0);
        ContextMessage upstream = Assert.Single(implement.Context.Seed,
            message => message.Source is AgentSource);

        Assert.Equal(2001, upstream.Text.Length);
        Assert.StartsWith("步骤「规划」的产出", upstream.Text);
        Assert.EndsWith("…", upstream.Text);
    }
}
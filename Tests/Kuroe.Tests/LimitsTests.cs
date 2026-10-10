using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
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
        string goal = new('目', 50);

        TaskSnapshot snapshot = harness.Snapshot(harness.Submit(goal));

        Assert.Equal(41, snapshot.Title.Length);
        Assert.StartsWith(new string('目', 40), snapshot.Title);
        Assert.EndsWith("…", snapshot.Title);
    }

    [Fact]
    public void Upstream_output_in_context_is_capped_with_an_ellipsis()
    {
        const string flow = """
            { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
              { "Name": "整体", "Nodes": [
                { "Name": "来源", "Model": "执行者", "Outputs": ["结论"] },
                { "Name": "接收", "Model": "执行者", "From": ["来源@结论"] }
              ] }
            ] } ] }
            """;

        using KuroeHarness harness = KuroeHarness.Create(flow);
        harness.Executor.Output = _ => new string('长', 3000);
        harness.Executor.PortValuesJsonFor = run => run.Context.NodeIndex == 1
            ? $$"""{"结论": "{{new string('长', 3000)}}"}"""
            : null;

        TaskSnapshot done = harness.Settle(harness.Submit("补齐 README"));
        RunSnapshot implement = done.Executables
            .Single(entry => entry.Executable.Name.Value == "接收")
            .Runs.Single();
        ContextMessage upstream = Assert.Single(implement.Context.Seed,
            message => message.Source is RunSource);

        Assert.Equal(2001, upstream.Text.Length);
        Assert.StartsWith("节点「来源」的端口「结论」产出", upstream.Text);
        Assert.EndsWith("…", upstream.Text);
    }
}

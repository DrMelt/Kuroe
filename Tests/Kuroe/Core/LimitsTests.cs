using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Xunit;

namespace Kuroe.Tests;

/// <summary>过程与上下文的上限收口。</summary>
public sealed class LimitsTests
{
    [Fact]
    public void Title_is_trimmed_to_the_first_line()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        TaskSnapshot snapshot = environment.Snapshot(environment.Submit("第一行\n第二行"));

        Assert.Equal("第一行", snapshot.Title);
    }

    [Fact]
    public void Title_is_truncated_at_forty_characters()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        string goal = new('目', 50);

        TaskSnapshot snapshot = environment.Snapshot(environment.Submit(goal));

        Assert.Equal(41, snapshot.Title.Length);
        Assert.StartsWith(new string('目', 40), snapshot.Title);
        Assert.EndsWith("…", snapshot.Title);
    }

    [Fact]
    public void Explicit_title_is_truncated_at_forty_characters()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        string title = new('标', 50);

        TaskId id = environment.Tasks.Submit("目标", new FlowName("默认"), title).ThrowIfError().Id;

        TaskSnapshot snapshot = environment.Snapshot(id);
        Assert.Equal(41, snapshot.Title.Length);
        Assert.StartsWith(new string('标', 40), snapshot.Title);
        Assert.EndsWith("…", snapshot.Title);
    }

    [Fact]
    public void Upstream_output_in_context_is_capped_with_an_ellipsis()
    {
        const string flow = """
            { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
              { "Name": "整体", "Kind": "Container", "Nodes": [
                { "Name": "来源", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
                { "Name": "接收", "Kind": "Llm", "Model": "执行者", "From": [ { "结论": "来源@结论" } ] }
              ] }
            ] } ] }
            """;

        using TestEnvironment environment = TestEnvironment.Create(flow);
        environment.Runner.Reply = _ => new string('长', 3000);
        environment.Runner.PortValuesJsonFor = execution => execution.Input.Node.Label.Value == "来源"
            ? $$"""{"结论": "{{new string('长', 3000)}}"}"""
            : null;

        TaskSnapshot done = environment.Settle(environment.Submit("补齐 README"));
        ExecutionSnapshot implement = done.Executables
            .Single(entry => entry.Executable.Label.Value == "接收")
            .Executions.Single();
        ContextValue upstream = Assert.Single(implement.Input.Inputs,
            value => value.Origin is ExecutionSource);

        Assert.Equal(2001, upstream.Text.Length);
        Assert.EndsWith("…", upstream.Text);
    }
}

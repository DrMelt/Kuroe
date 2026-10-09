using ApiHub.Shared.Models;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Workflows.Flows;
using Xunit;

namespace Kuroe.Shared.Tests;

/// <summary>上下文模型：run 的叫法、条目序号与出处的可读文本。</summary>
public sealed class ContextModelTests
{
    [Fact]
    public void RunContext_label_merges_step_and_item()
    {
        RunContext stepLevel = Context() with { ItemIndex = null };
        RunContext itemLevel = Context() with { ItemIndex = 2 };

        Assert.Equal("实施", stepLevel.Label);
        Assert.Equal("实施·条目 3", itemLevel.Label);
    }

    [Fact]
    public void RunContext_defaults_execution_count_and_seed()
    {
        RunContext context = Context();

        Assert.Equal(1, context.ExecutionCount);
        Assert.Empty(context.Seed);
    }

    [Fact]
    public void RunSource_points_back_to_its_run()
    {
        RunId run = new(7);
        RunSource source = new(run, new NodeName("实施"));

        Assert.Equal("run #7 · 实施 产出", source.Label);
        Assert.Equal(run, source.FromRun);
    }

    [Fact]
    public void ItemSource_points_back_to_plan()
    {
        RunId plan = new(1);
        ItemSource source = new(plan, 0, "甲");

        Assert.Equal("run #1 · 规划条目 1：甲", source.Label);
        Assert.Equal(plan, source.FromRun);
    }

    private static RunContext Context() => new()
    {
        Task = new TaskId(1),
        Output = NodeOutput.Text,
        NodeIndex = 1,
        NodeName = new NodeName("实施"),
        Instruction = "做事",
        Model = ModelName.Create("fake").Value,
    };
}

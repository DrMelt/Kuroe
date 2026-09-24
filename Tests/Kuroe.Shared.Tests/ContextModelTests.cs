using Kuroe.Shared.Agent;
using Xunit;

namespace Kuroe.Shared.Tests;

/// <summary>上下文模型：agent 的叫法、条目序号与出处的可读文本。</summary>
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
    public void RunContext_defaults_attempt_and_seed()
    {
        RunContext context = Context();

        Assert.Equal(1, context.Attempt);
        Assert.Empty(context.Seed);
    }

    [Fact]
    public void AgentSource_points_back_to_its_run()
    {
        RunId run = new(7);
        AgentSource source = new(run, "实施");

        Assert.Equal("agent #7 · 实施 产出", source.Label);
        Assert.Equal(run, source.FromRun);
    }

    [Fact]
    public void DialogueSource_has_no_jump_target()
    {
        DialogueSource source = new(new TaskId(3), 2);

        Assert.Equal("任务 #3 第 2 回合", source.Label);
        Assert.Null(source.FromRun);
    }

    [Fact]
    public void ItemSource_points_back_to_plan()
    {
        RunId plan = new(1);
        ItemSource source = new(plan, 0, "甲");

        Assert.Equal("agent #1 · 规划条目 1：甲", source.Label);
        Assert.Equal(plan, source.FromRun);
    }

    private static RunContext Context() => new()
    {
        Task = new TaskId(1),
        Role = RunRole.Implement,
        StepIndex = 1,
        StepName = "实施",
        Instruction = "做事",
        Model = "fake",
    };
}
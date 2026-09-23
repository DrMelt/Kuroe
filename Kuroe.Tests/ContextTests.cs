using Kuroe.Agent;
using Kuroe.Workflows;
using Xunit;

namespace Kuroe.Tests;

/// <summary>上下文只由上游装配：每个 agent 看到的内容与出处都可追溯。</summary>
public sealed class ContextTests
{
    [Fact]
    public void First_step_gets_goal_as_instruction_and_no_upstream_seed()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        TaskSnapshot done = harness.Settle(harness.Submit("补齐 README"));
        RunSnapshot plan = Assert.Single(done.Steps[0].Runs);

        Assert.Contains("补齐 README", plan.Context.Instruction);
        Assert.Equal("fake", plan.Context.Model);
        Assert.Empty(plan.Context.Seed);
    }

    [Fact]
    public void Implement_step_context_carries_plan_output_and_its_own_item()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        TaskSnapshot done = harness.Settle(harness.Submit("补齐 README"));
        RunSnapshot plan = Assert.Single(done.Steps[0].Runs);
        RunSnapshot implement = Assert.Single(done.Steps[1].Runs, run => run.Context.ItemIndex == 0);

        Assert.Contains(implement.Context.Seed, message =>
            message.Source is AgentSource { StepName: "规划", FromRun: not null } source && source.FromRun == plan.Id);
        Assert.Contains(implement.Context.Seed, message =>
            message.Source is ItemSource { Index: 0 } source && source.FromRun == plan.Id);
        Assert.Contains("条目 1", implement.Context.Instruction);

        string visible = implement.Context.Instruction
            + string.Concat(implement.Context.Seed.Select(message => message.Text));
        Assert.DoesNotContain("做乙", visible);
    }

    [Fact]
    public void Check_step_context_carries_the_output_it_reviews()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        TaskSnapshot done = harness.Settle(harness.Submit("补齐 README"));
        RunSnapshot implement = Assert.Single(done.Steps[1].Runs, run => run.Context.ItemIndex == 1);
        RunSnapshot check = Assert.Single(done.Steps[2].Runs, run => run.Context.ItemIndex == 1);

        Assert.Contains(check.Context.Seed, message =>
            message.Source is AgentSource { StepName: "实施" } source && source.FromRun == implement.Id);
        Assert.Equal(2, check.Context.Seed.Count(message => message.Source is AgentSource));
    }
}

using ApiHub.Shared.Models;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Tests;

/// <summary>上下文只由上游装配：每个 run 看到的内容与出处都可追溯。</summary>
public sealed class ContextTests
{
    [Fact]
    public void First_step_gets_goal_as_instruction_and_no_upstream_seed()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        TaskSnapshot done = harness.Settle(harness.Submit("补齐 README"));
        RunSnapshot plan = Assert.Single(done.Executables[0].Runs);

        Assert.Contains("补齐 README", plan.Context.Instruction);
        Assert.Equal(ModelName.Create("fake").Value, plan.Context.Model);
        Assert.Empty(plan.Context.Seed);
    }

    [Fact]
    public void Implement_item_context_carries_plan_output_and_its_own_item()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        TaskSnapshot done = harness.Settle(harness.Submit("补齐 README"));
        RunSnapshot plan = Assert.Single(done.Executables[0].Runs);
        RunSnapshot implement = Assert.Single(done.Executables[1].Runs, run => run.Context.ItemIndex == 0);

        Assert.Contains(implement.Context.Seed, message =>
            message.Source is RunSource { NodeName.Value: "制定计划", FromRun: not null } source && source.FromRun == plan.Id);
        Assert.Contains(implement.Context.Seed, message =>
            message.Source is ItemSource { Index: 0 } source && source.FromRun == plan.Id);
        Assert.Contains("条目 1", implement.Context.Instruction);

        string visible = implement.Context.Instruction
            + string.Concat(implement.Context.Seed.Select(message => message.Text));
        Assert.DoesNotContain("做乙", visible);
    }
}

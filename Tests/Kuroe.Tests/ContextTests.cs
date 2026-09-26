using Kuroe.Shared.Agent;
using Kuroe.Shared.Agent.Runs;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
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
        RunSnapshot plan = Assert.Single(done.Nodes[0].Runs);

        Assert.Contains("补齐 README", plan.Context.Instruction);
        Assert.Equal("fake", plan.Context.Model);
        Assert.Empty(plan.Context.Seed);
    }

    [Fact]
    public void Implement_item_context_carries_plan_output_and_its_own_item()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        TaskSnapshot done = harness.Settle(harness.Submit("补齐 README"));
        RunSnapshot plan = Assert.Single(done.Nodes[0].Runs);
        RunSnapshot implement = Assert.Single(done.Nodes[1].Runs, run => run.Context.ItemIndex == 0);

        Assert.Contains(implement.Context.Seed, message =>
            message.Source is AgentSource { NodeName: "制定计划", FromRun: not null } source && source.FromRun == plan.Id);
        Assert.Contains(implement.Context.Seed, message =>
            message.Source is ItemSource { Index: 0 } source && source.FromRun == plan.Id);
        Assert.Contains("条目 1", implement.Context.Instruction);

        string visible = implement.Context.Instruction
            + string.Concat(implement.Context.Seed.Select(message => message.Text));
        Assert.DoesNotContain("做乙", visible);
    }

    [Fact]
    public void Funnel_check_context_carries_every_item_output_it_reviews()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        TaskSnapshot done = harness.Settle(harness.Submit("补齐 README"));
        RunSnapshot check = Assert.Single(done.Nodes[2].Runs);

        Assert.Equal(NodeOutput.Review, check.Context.Output);
        Assert.Null(check.Context.ItemIndex);
        // 规划产出一份，各条目实施产出各一份
        Assert.Contains(check.Context.Seed, message =>
            message.Source is AgentSource { NodeName: "制定计划" });
        Assert.Equal(2, check.Context.Seed.Count(message =>
            message.Source is AgentSource { NodeName: "分配执行" }));
        Assert.Equal(3, check.Context.Seed.Count(message => message.Source is AgentSource));

        string visible = check.Context.Instruction
            + string.Concat(check.Context.Seed.Select(message => message.Text));
        Assert.Contains("条目「甲」", visible);
        Assert.Contains("条目「乙」", visible);
    }
}

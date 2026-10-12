using ApiHub.Shared.Models;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Tests;

/// <summary>上下文只由上游装配：每个 run 看到的内容与出处都可追溯。</summary>
public sealed class ContextTests
{
    [Fact]
    public void First_step_gets_goal_from_start_port_and_no_other_upstream_seed()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        TaskSnapshot done = harness.Settle(harness.Submit("补齐 README"));
        RunSnapshot plan = Assert.Single(done.Executables[0].Runs);

        ContextMessage goal = Assert.Single(plan.Context.Seed);
        Assert.Equal("补齐 README", goal.Text);
        Assert.IsType<StartSource>(goal.Source);
        Assert.Equal(ModelName.Create("fake").Value, plan.Context.Model);
    }

    [Fact]
    public void Implement_item_context_carries_plan_output_and_its_own_item()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        TaskSnapshot done = harness.Settle(harness.Submit("补齐 README"));
        RunSnapshot plan = Assert.Single(done.Executables[0].Runs);
        RunSnapshot implement = Assert.Single(done.Executables[1].Runs, run => run.Context.ItemIndex == 0);

        Assert.Contains(implement.Context.Seed, message =>
            message.Source is ItemSource { Index: 0 } source && source.FromRun == plan.Id);
        Assert.Contains("条目 1", implement.Context.Instruction);
        // 拆分边只驱动实例展开，不把规划文本注入实例上下文
        Assert.DoesNotContain(implement.Context.Seed, message => message.Text.Contains("节点「制定计划」"));

        string visible = implement.Context.Instruction
            + string.Concat(implement.Context.Seed.Select(message => message.Text));
        Assert.DoesNotContain("做乙", visible);
    }

    [Fact]
    public void Start_constant_port_feeds_downstream_instead_of_goal()
    {
        using KuroeHarness harness = KuroeHarness.Create(StartConstantFlow);

        TaskSnapshot done = harness.Settle(harness.Submit("补齐 README"));
        RunSnapshot run = Assert.Single(done.Executables[0].Runs);

        ContextMessage seed = Assert.Single(run.Context.Seed);
        Assert.Equal("固定背景", seed.Text);
        Assert.Equal(new PortName("背景"), Assert.IsType<StartSource>(seed.Source).Port);
    }

    /// <summary>开始除目标外声明一个带固定值的端口，下游取固定值端口。</summary>
    private const string StartConstantFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "背景", "Type": "Text", "Value": "固定背景" }] },
              "Nodes": [
                { "Name": "整体", "Kind": "Container", "Nodes": [
                  { "Name": "干活", "Kind": "Executable", "Model": "执行者", "From": [ { "背景": "Start@背景" } ] }
                ] }
              ]
            }
          ]
        }
        """;
}

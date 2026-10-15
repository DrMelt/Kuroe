using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Xunit;

namespace Kuroe.Tests;

/// <summary>推进引擎的调度时序：run 收口即推下游，不因同层无关的慢 run 阻塞；
/// 活跃屏障使来源新版本通知丢失后，run 收口必须补一次评估把节点放行。</summary>
public sealed class EngineAdvanceTests
{
    [Fact]
    public void Downstream_starts_while_unrelated_slow_run_is_running()
    {
        using KuroeHarness harness = KuroeHarness.Create(SlowParallelFlow);
        harness.Executor.DelayFor = run => run.Context.NodeName.Value == "慢工" ? 500 : 10;

        TaskId id = harness.Submit("并行推进");

        // 慢 run 的下游链末端已启动，慢 run 自身应尚未收口
        harness.Wait(id, snapshot => snapshot.Executables.Any(node =>
            node.Executable.Name.Value == "收官" && node.Runs.Count > 0));

        Assert.Contains(harness.Registry.LiveRuns(), run => run.Context.NodeName.Value == "慢工");

        Assert.Equal(TaskState.Done, harness.Settle(id).State);
    }

    /// <summary>多组各自独立的汇合：前一个来源先发布拉起汇合 run，后一个来源在 run 在跑时发布，
    /// 其通知因活跃屏障丢失时，汇合 run 收口后必须补评估重新启动，否则任务停在运行态。</summary>
    [Fact]
    public void Groups_advance_past_interleaved_source_release()
    {
        using KuroeHarness harness = KuroeHarness.Create(IndependentGroupsFlow);
        harness.Executor.DelayMs = 30;

        for (int i = 0; i < 10; i++)
        {
            TaskId id = harness.Submit("补齐 README");
            TaskSnapshot done = harness.Settle(id);

            Assert.Equal(TaskState.Done, done.State);
            Assert.NotEmpty(done.Executables[2].Runs);
        }
    }

    private const string SlowParallelFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "执行者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "切入", "Kind": "Executable", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
                    { "Name": "慢工", "Kind": "Executable", "Model": "执行者", "From": [ { "目标": "Start@目标" } ] },
                    { "Name": "衔接", "Kind": "Executable", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "结论": "切入@结论" } ] },
                    { "Name": "收官", "Kind": "Executable", "Model": "执行者", "From": [ { "结论": "衔接@结论" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>两个组各自独立：甲、乙各自齐备即启动一次汇合 run。</summary>
    private const string IndependentGroupsFlow = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "甲", "Kind": "Executable", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
            { "Name": "乙", "Kind": "Executable", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
            { "Name": "汇合", "Kind": "Executable", "Model": "执行者", "From": [ { "甲": "甲@结论" }, { "乙": "乙@结论" } ] }
          ] }
        ] } ] }
        """;
}

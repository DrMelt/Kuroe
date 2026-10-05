using Kuroe.Shared.Executions;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
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

    /// <summary>AnyOf 组内重复来源的汇合：某来源先发布拉起汇合 run，另一来源随后发布的通知
    /// 因活跃屏障丢失时，汇合 run 收口后必须补评估重新启动，否则任务停在运行态。</summary>
    [Fact]
    public void AnyOf_repeats_advance_past_interleaved_source_release()
    {
        using KuroeHarness harness = KuroeHarness.Create(AnyOfRepeatsFlow);
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
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                    { "Name": "切入", "Model": "执行者" },
                    { "Name": "慢工", "Model": "执行者" },
                    { "Name": "衔接", "Model": "执行者", "From": ["切入"] },
                    { "Name": "收官", "Model": "执行者", "From": ["衔接"] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>AnyOf 组内与组间重复引用来源：甲独占一组并组内重复，甲与乙组成的另一组跨组共享甲。</summary>
    private const string AnyOfRepeatsFlow = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "甲", "Model": "执行者" },
            { "Name": "乙", "Model": "执行者" },
            { "Name": "汇合", "Model": "执行者", "AnyOf": [["甲", "甲"], ["甲", "乙"]] }
          ] }
        ] } ] }
        """;
}

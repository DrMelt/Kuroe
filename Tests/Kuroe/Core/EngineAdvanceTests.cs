using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Xunit;

namespace Kuroe.Tests;

/// <summary>推进引擎的调度时序：执行收口即推下游，不因同层无关的慢执行阻塞；
/// 活跃屏障使来源新版本通知丢失后，执行收口必须补一次评估把节点放行。</summary>
public sealed class EngineAdvanceTests
{
    [Fact]
    public void Downstream_starts_while_unrelated_slow_execution_is_running()
    {
        using TestEnvironment environment = TestEnvironment.Create(SlowParallelFlow);
        environment.Runner.DelayFor = execution => execution.Input.Node.Label.Value == "慢工" ? 500 : 10;

        TaskId id = environment.Submit("并行推进");

        // 慢执行的下游链末端已启动，慢执行自身应尚未收口
        environment.Wait(id, snapshot => snapshot.Executables.Any(node =>
            node.Executable.Label.Value == "收官" && node.Executions.Count > 0));

        Assert.Contains(environment.Registry.LiveExecutions(), execution => execution.Input.Node.Label.Value == "慢工");

        Assert.Equal(TaskState.Done, environment.Settle(id).State);
    }

    /// <summary>多组各自独立的汇合：前一个来源先发布拉起汇合执行，后一个来源在执行在跑时发布，
    /// 其通知因活跃屏障丢失时，汇合执行收口后必须补评估重新启动，否则任务停在运行态。</summary>
    [Fact]
    public void Groups_advance_past_interleaved_source_release()
    {
        using TestEnvironment environment = TestEnvironment.Create(IndependentGroupsFlow);
        environment.Runner.DelayMs = 30;

        for (int i = 0; i < 10; i++)
        {
            TaskId id = environment.Submit("补齐 README");
            TaskSnapshot done = environment.Settle(id);

            Assert.Equal(TaskState.Done, done.State);
            Assert.NotEmpty(done.Executables[2].Executions);
        }
    }

    private const string SlowParallelFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    { "Name": "切入", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                    { "Name": "慢工", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] },
                    { "Name": "衔接", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "结论": "切入@结论" } ] },
                    { "Name": "收官", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "结论": "衔接@结论" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>两个组各自独立：甲、乙各自齐备即启动一次汇合执行。</summary>
    private const string IndependentGroupsFlow = """
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
            { "Name": "甲", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
            { "Name": "乙", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
            { "Name": "汇合", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "甲": "甲@结论" }, { "乙": "乙@结论" } ] }
          ] }
        ] } ] }
        """;
}

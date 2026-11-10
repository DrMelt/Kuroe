using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Xunit;

namespace Kuroe.Integration.Tests.Runtime.Engine;

/// <summary>推进引擎的调度时序：执行收口即推下游，不因同层无关的慢执行阻塞；
/// 活跃屏障使来源新版本通知丢失后，执行收口必须补一次评估把节点放行。</summary>
public sealed class EngineAdvanceTests
{
    [Fact]
    public async Task Downstream_starts_while_unrelated_slow_execution_is_running()
    {
        using TestEnvironment environment = TestEnvironment.Create(SlowParallelFlow);
        environment.Runner.Gate.Hold(NodePath.Of("整体", "慢工"));

        TaskId id = environment.Submit("并行推进");

        try
        {
            // 慢执行的下游链末端已启动，慢执行自身应尚未收口
            await environment.WaitAsync(id, snapshot => snapshot.Executables.Any(node =>
                node.Executable.Label.Value == "收官" && node.Executions.Count > 0));

            Assert.Contains(environment.TaskReader.LiveExecutions(), execution => execution.Input.Node.Label.Value == "慢工");
        }
        finally
        {
            environment.Runner.Gate.ClearHolds();
        }

        Assert.Equal(TaskState.Done, (await environment.SettleAsync(id)).State);
    }

    /// <summary>多组各自独立的汇合：前一个来源先发布拉起汇合执行，后一个来源在执行在跑时发布，
    /// 其通知因活跃屏障丢失时，汇合执行收口后必须补评估重新启动，否则任务停在运行态。</summary>
    [Fact]
    public async Task Groups_advance_past_interleaved_source_release()
    {
        using TestEnvironment environment = TestEnvironment.Create(IndependentGroupsFlow);
        NodePath second = NodePath.Of("整体", "乙");
        NodePath merge = NodePath.Of("整体", "汇合");
        environment.Runner.Gate.Hold(second);
        environment.Runner.Gate.Hold(merge);

        TaskId id = environment.Submit("补齐 README");

        // 甲先发布拉起汇合，乙在汇合运行期间发布
        await environment.Runner.Gate.Entered(merge);
        environment.Runner.Gate.Open(second);
        await environment.WaitAsync(id, snapshot => snapshot.Executables.Any(node =>
            node.Executable.Label.Value == "乙"
            && node.Executions.Any(execution => execution.State == ExecutionState.Succeeded)));

        environment.Runner.Gate.Open(merge);

        TaskSnapshot done = await environment.SettleAsync(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.NotEmpty(done.Executables[2].Executions);
    }

    private const string SlowParallelFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }],
              "Root": {
                "Name": "整体",
                "Kind": "Container",
                "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
                  { "Name": "切入", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                  { "Name": "慢工", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] },
                  { "Name": "衔接", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "结论": "切入@结论" } ] },
                  { "Name": "收官", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "结论": "衔接@结论" } ] }
                ]
              }
            }
          ]
        }
        """;

    /// <summary>两个组各自独立：甲、乙各自齐备即启动一次汇合执行。</summary>
    private const string IndependentGroupsFlow = """
        { "Flows": [ { "Name": "默认", "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }], "Root": { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
          { "Name": "甲", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
          { "Name": "乙", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
          { "Name": "汇合", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "甲": "甲@结论" }, { "乙": "乙@结论" } ] }
        ] } } ] }
        """;
}

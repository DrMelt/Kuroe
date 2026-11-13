using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Node.RuntimeGraph.Execution;
using Kuroe.Flow.Node.RuntimeGraph.Tasks;
using Xunit;

namespace Kuroe.Integration.Tests.Runtime.Engine;

/// <summary>执行次数按执行节点累计，以启动的执行记录为准。</summary>
public sealed class ExecutionCountTests
{
    /// <summary>失败后重新执行一次，节点执行次数按启动的执行累计。</summary>
    [Fact]
    public void Rerun_increments_the_node_execution_count()
    {
        using TestEnvironment environment = TestEnvironment.Create(SingleNodeFlow);
        environment.Runner.FailsWhen = execution => execution.Input.ExecutionCount == 1;

        TaskId id = environment.Submit("补齐 README");
        environment.Wait(id, snapshot => snapshot.State == TaskState.Blocked);
        environment.Tasks.Rerun(id).ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(2, Executions(task, "干活"));
    }

    /// <summary>上游阻塞时下游不启动，统计保持 0。</summary>
    [Fact]
    public void Blocked_upstream_leaves_downstream_on_zero()
    {
        using TestEnvironment environment = TestEnvironment.Create(ChainFlow);
        environment.Runner.FailsWhen = execution => execution.Input.Node.Label.Value == "上游";

        TaskId id = environment.Submit("补齐 README");
        environment.Wait(id, snapshot => snapshot.State == TaskState.Blocked);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(1, Executions(task, "上游"));
        Assert.Equal(0, Executions(task, "下游"));
    }

    /// <summary>单个执行节点，失败后重新执行。</summary>
    private const string SingleNodeFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }],
              "Root": {
                "Name": "整体",
                "Kind": "Container",
                "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
                  { "Name": "干活", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] }
                ]
              }
            }
          ]
        }
        """;

    /// <summary>上游阻塞时下游不启动，统计保持 0。</summary>
    private const string ChainFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }],
              "Root": {
                "Name": "整体",
                "Kind": "Container",
                "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
                  { "Name": "上游", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                  { "Name": "下游", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "结论": "上游@结论" } ] }
                ]
              }
            }
          ]
        }
        """;

    /// <summary>执行节点按启动的执行记录计数。</summary>
    private static int Executions(WorkTask task, string label) =>
        task.Snapshot().View.Steps
            .Single(step => step.Label == label)
            .Executions.Count;
}
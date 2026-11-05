using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Xunit;

namespace Kuroe.Integration.Tests.Runtime.Engine;

/// <summary>执行器上报的用量进入执行快照，运行中为当前累计，收口后为最终值。</summary>
public sealed class ExecutionUsageTests
{
    [Fact]
    public void Runner_usage_reaches_the_execution_snapshot()
    {
        using TestEnvironment environment = TestEnvironment.Create(SingleNodeFlow);
        ExecutionUsage usage = new(3, 1, new TokenUsage(120, 30, 150, 80, 5));
        environment.Runner.Usage = usage;

        TaskId id = environment.Submit("统计用量");
        TaskSnapshot done = environment.Settle(id);

        ExecutionSnapshot execution = Assert.Single(Assert.Single(done.Executables).Executions);
        Assert.Equal(usage, execution.Usage);
    }

    [Fact]
    public void Runner_usage_is_visible_while_the_execution_runs()
    {
        using TestEnvironment environment = TestEnvironment.Create(SingleNodeFlow);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ExecutionUsage usage = new(1, 0, new TokenUsage(12, 3, 15, 4, 1));
        environment.Runner.Usage = usage;
        environment.Runner.HoldFor = _ => release.Task;

        TaskId id = environment.Submit("统计用量");
        TaskSnapshot running = environment.Wait(
            id,
            snapshot => snapshot.Executables
                .SelectMany(node => node.Executions)
                .Any(execution => execution.State == ExecutionState.Running));

        Assert.Equal(usage, Assert.Single(Assert.Single(running.Executables).Executions).Usage);

        release.SetResult();
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(usage, Assert.Single(Assert.Single(done.Executables).Executions).Usage);
    }

    /// <summary>单个执行节点。</summary>
    private const string SingleNodeFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] },
              "Root": {
                "Name": "整体",
                "Kind": "Container",
                "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "Start@目标" } ], "Bindings": { "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                  { "Name": "干活", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] }
                ]
              }
            }
          ]
        }
        """;
}
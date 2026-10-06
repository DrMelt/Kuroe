using ErrorOr;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Graph;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Tests;

/// <summary>输入节点：任务停在待输入、回答即产出放行下游、回答进入下游上下文。</summary>
public sealed class InputNodeTests
{
    /// <summary>等待全部输入节点停驻后返回快照。</summary>
    private static TaskSnapshot WaitForInputs(KuroeHarness harness, TaskId id, int count) =>
        harness.Wait(id, snapshot => snapshot.ExecutableStates.Count(state => state.State == NodeState.AwaitingInput) == count);

    [Fact]
    public void Input_node_parks_until_answered_then_releases_downstream()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.InputFlow);

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot parked = WaitForInputs(harness, id, 1);

        Assert.Equal(TaskState.AwaitingInput, parked.State);
        ExecutableStateSnapshot input = Assert.Single(parked.ExecutableStates, state => state.State == NodeState.AwaitingInput);
        Assert.Empty(input.AwaitingRuns);

        harness.Tasks.Answer(id, null, "需要支持多语言").ThrowIfError();

        TaskSnapshot done = harness.Settle(id);
        Assert.Equal(TaskState.Done, done.State);
        Assert.DoesNotContain(done.ExecutableStates, state => state.State == NodeState.AwaitingInput);
    }

    [Fact]
    public void Answer_feeds_downstream_run_context()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.InputFlow);

        TaskId id = harness.Submit("补齐 README");
        WaitForInputs(harness, id, 1);

        harness.Tasks.Answer(id, null, "需要支持多语言").ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        RunSnapshot implement = done.Executables
            .Single(entry => entry.Executable.Name.Value == "实施")
            .Runs.Single();
        Assert.Contains(implement.Context.Seed,
            message => message.Text.Contains("需要支持多语言")
                && message.Text.Contains("用户输入"));
    }

    [Fact]
    public void Answer_without_pending_input_fails()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.InputFlow);

        TaskId id = harness.Submit("补齐 README");
        WaitForInputs(harness, id, 1);

        harness.Tasks.Answer(id, null, "x").ThrowIfError();
        harness.Settle(id);

        ErrorOr<Success> answered = harness.Tasks.Answer(id, null, "又");
        Assert.True(answered.IsError);
        Assert.Contains(answered.ErrorsOrEmptyList, error => error.Description.Contains("没有等待回答的输入节点"));
    }

    [Fact]
    public void Two_input_nodes_require_node_name()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.TwoInputFlow);

        TaskId id = harness.Submit("目标");
        WaitForInputs(harness, id, 2);

        ErrorOr<Success> ambiguous = harness.Tasks.Answer(id, null, "x");
        Assert.True(ambiguous.IsError);
        Assert.Contains(ambiguous.ErrorsOrEmptyList, error => error.Description.Contains("有 2 个输入节点等待回答"));

        harness.Tasks.Answer(id, "输入一", "甲").ThrowIfError();
        TaskSnapshot stepped = WaitForInputs(harness, id, 1);
        Assert.Equal(TaskState.AwaitingInput, stepped.State);

        harness.Tasks.Answer(id, "输入二", "乙").ThrowIfError();
        TaskSnapshot done = harness.Settle(id);
        Assert.Equal(TaskState.Done, done.State);
    }

    [Fact]
    public void Input_node_with_upstream_parks_after_upstream_releases()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.InputFromUpstreamFlow);

        TaskId id = harness.Submit("目标");
        TaskSnapshot parked = WaitForInputs(harness, id, 1);

        Assert.Equal(TaskState.AwaitingInput, parked.State);
        Assert.Single(harness.Executor.Started,
            run => run.Context.NodeName.Value == "准备" && run.State == RunState.Succeeded);

        harness.Tasks.Answer(id, "用户输入", "回答").ThrowIfError();
        TaskSnapshot done = harness.Settle(id);
        Assert.Equal(TaskState.Done, done.State);
    }

    [Fact]
    public void Empty_answer_is_rejected_without_consuming_waiting()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.InputFlow);

        TaskId id = harness.Submit("补齐 README");
        WaitForInputs(harness, id, 1);

        ErrorOr<Success> empty = harness.Tasks.Answer(id, null, "   ");
        Assert.True(empty.IsError);
        Assert.Contains(empty.ErrorsOrEmptyList, error => error.Description.Contains("回答内容不能为空"));

        TaskSnapshot still = harness.Snapshot(id);
        Assert.Equal(TaskState.AwaitingInput, still.State);
        Assert.Single(still.ExecutableStates, state => state.State == NodeState.AwaitingInput);
    }

    [Fact]
    public void Input_node_carries_no_model_definition()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.InputFlow);

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot parked = WaitForInputs(harness, id, 1);

        ExecutableNode input = parked.Graph.ExecutableNodes.Single(node => node.Name.Value == "用户输入");
        ExecutableNode implement = parked.Graph.ExecutableNodes.Single(node => node.Name.Value == "实施");

        Assert.Null(input.Model);
        Assert.NotNull(implement.Model);
    }

    [Fact]
    public void Answer_releases_auto_container_downstream()
    {
        using KuroeHarness harness = KuroeHarness.Create(InputInAutoContainerFlow);

        TaskId id = harness.Submit("补齐 README");
        WaitForInputs(harness, id, 1);

        harness.Tasks.Answer(id, null, "需要支持多语言").ThrowIfError();

        TaskSnapshot done = harness.Settle(id);
        Assert.Equal(TaskState.Done, done.State);
        ExecutableSnapshot implement = Assert.Single(done.Executables, entry => entry.Executable.Name.Value == "实施");
        Assert.Single(implement.Runs);
    }

    [Fact]
    public void Answer_parks_review_container_until_approve()
    {
        using KuroeHarness harness = KuroeHarness.Create(InputInReviewContainerFlow);

        TaskId id = harness.Submit("补齐 README");
        WaitForInputs(harness, id, 1);

        harness.Tasks.Answer(id, null, "需要支持多语言").ThrowIfError();

        TaskSnapshot awaiting = harness.Settle(id);
        Assert.Equal(TaskState.AwaitingApproval, awaiting.State);
        Assert.Equal(NodeState.AwaitingApproval, Assert.Single(awaiting.Containers, container => container.Name == "审查容器").State);

        harness.Tasks.Approve(id).ThrowIfError();

        TaskSnapshot done = harness.Settle(id);
        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(NodeState.Done, Assert.Single(done.Containers, container => container.Name == "审查容器").State);
    }

    [Fact]
    public void Cancel_while_awaiting_input_ends_task()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.TwoInputFlow);

        TaskId id = harness.Submit("目标");
        WaitForInputs(harness, id, 2);

        harness.Tasks.StopTask(id).ThrowIfError();

        TaskSnapshot canceled = harness.Wait(id, snapshot => snapshot.State == TaskState.Canceled);
        Assert.Equal(TaskState.Canceled, canceled.State);
    }

    [Fact]
    public void Answer_after_cancel_fails()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.TwoInputFlow);

        TaskId id = harness.Submit("目标");
        WaitForInputs(harness, id, 2);

        harness.Tasks.StopTask(id).ThrowIfError();
        harness.Wait(id, snapshot => snapshot.State == TaskState.Canceled);

        ErrorOr<Success> answered = harness.Tasks.Answer(id, null, "甲");
        Assert.True(answered.IsError);
        Assert.Contains(answered.ErrorsOrEmptyList, error => error.Description.Contains("没有等待回答的输入节点"));
    }

    [Fact]
    public void Container_with_parked_input_shows_awaiting_input()
    {
        using KuroeHarness harness = KuroeHarness.Create(InputInAutoContainerFlow);

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot parked = WaitForInputs(harness, id, 1);

        // 直接含输入节点的容器与沿祖先链的外层容器都停在待输入
        Assert.All(parked.Containers, container => Assert.Equal(NodeState.AwaitingInput, container.State));
    }

    [Fact]
    public void Parallel_review_and_input_waits_coexist()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.ParallelInputAndReviewFlow);

        TaskId id = harness.Submit("目标");
        TaskSnapshot parked = harness.Wait(id, snapshot =>
            snapshot.ExecutableStates.Any(state => state.State == NodeState.AwaitingInput)
            && snapshot.Containers.Any(container => container.Name == "审查分支" && container.State == NodeState.AwaitingApproval));

        // 任务级汇总只表达优先的待输入，两个等待在快照里各自可见
        Assert.Equal(TaskState.AwaitingInput, parked.State);
        Assert.Contains(parked.ExecutableStates, state => state.State == NodeState.AwaitingInput);
        Assert.Contains(parked.Containers, container => container.Name == "审查分支" && container.State == NodeState.AwaitingApproval);
    }

    /// <summary>输入节点包在自动容器里，回答后容器放行，下游实施消费回答。</summary>
    private const string InputInAutoContainerFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                    {
                      "Name": "收集",
                      "Nodes": [
                        { "Name": "用户输入", "Output": "Input", "Question": "请补充背景" }
                      ]
                    },
                    { "Name": "实施", "Output": "Text", "Model": "执行者", "From": ["收集"] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>输入节点包在带 Review 门控的容器里，回答后容器停在待批准，批准后放行。</summary>
    private const string InputInReviewContainerFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                    {
                      "Name": "审查容器",
                      "Gate": "Review",
                      "Nodes": [
                        { "Name": "用户输入", "Output": "Input", "Question": "请补充背景" }
                      ]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;
}

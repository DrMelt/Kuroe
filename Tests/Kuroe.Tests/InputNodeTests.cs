using ErrorOr;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Runs;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.Shared.Waiting;
using Xunit;

namespace Kuroe.Tests;

/// <summary>输入节点：任务停在待输入、回答即产出放行下游、回答进入下游上下文。</summary>
public sealed class InputNodeTests
{
    /// <summary>等待全部输入节点停驻后返回快照。</summary>
    private static TaskSnapshot WaitForInputs(TestEnvironment environment, TaskId id, int count) =>
        environment.Wait(id, _ => environment.Awaiting(id, AwaitKind.Answer).Count == count);

    [Fact]
    public void Input_node_parks_until_answered_then_releases_downstream()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.InputFlow);

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot parked = WaitForInputs(environment, id, 1);

        Assert.Equal(TaskState.Running, parked.State);
        Assert.Single(environment.Awaiting(id, AwaitKind.Answer));

        environment.Waits.Answer(id, null, "需要支持多语言").ThrowIfError();

        TaskSnapshot done = environment.Settle(id);
        Assert.Equal(TaskState.Done, done.State);
        Assert.Empty(environment.Awaiting(id, AwaitKind.Answer));
    }

    [Fact]
    public void Answer_feeds_downstream_run_context()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.InputFlow);

        TaskId id = environment.Submit("补齐 README");
        WaitForInputs(environment, id, 1);

        environment.Waits.Answer(id, null, "需要支持多语言").ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

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
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.InputFlow);

        TaskId id = environment.Submit("补齐 README");
        WaitForInputs(environment, id, 1);

        environment.Waits.Answer(id, null, "x").ThrowIfError();
        environment.Settle(id);

        ErrorOr<Success> answered = environment.Waits.Answer(id, null, "又");
        Assert.True(answered.IsError);
        Assert.Contains(answered.ErrorsOrEmptyList, error => error.Description.Contains("没有等待回答的输入节点"));
    }

    [Fact]
    public void Two_input_nodes_require_node_name()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.TwoInputFlow);

        TaskId id = environment.Submit("目标");
        WaitForInputs(environment, id, 2);

        ErrorOr<Success> ambiguous = environment.Waits.Answer(id, null, "x");
        Assert.True(ambiguous.IsError);
        Assert.Contains(ambiguous.ErrorsOrEmptyList, error => error.Description.Contains("有 2 个输入节点等待回答"));

        environment.Waits.Answer(id, "整体/输入一", "甲").ThrowIfError();
        TaskSnapshot stepped = WaitForInputs(environment, id, 1);
        Assert.Equal(TaskState.Running, stepped.State);

        environment.Waits.Answer(id, "整体/输入二", "乙").ThrowIfError();
        TaskSnapshot done = environment.Settle(id);
        Assert.Equal(TaskState.Done, done.State);
    }

    /// <summary>有上游的输入节点提交即停驻等待回答，上游发布后作答放行下游。</summary>
    [Fact]
    public void Input_node_with_upstream_answers_after_upstream_releases()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.InputFromUpstreamFlow);

        TaskId id = environment.Submit("目标");
        _ = environment.Wait(id, snapshot =>
            environment.Executor.Started.Any(run => run.Context.Node.Name.Value == "准备" && run.State == RunState.Succeeded)
            && environment.Awaiting(id, AwaitKind.Answer).Count > 0);
        TaskSnapshot parked = environment.Snapshot(id);

        Assert.Equal(TaskState.Running, parked.State);

        environment.Waits.Answer(id, "整体/用户输入", "回答").ThrowIfError();
        TaskSnapshot done = environment.Settle(id);
        Assert.Equal(TaskState.Done, done.State);
    }

    /// <summary>挂点环中输入节点每轮回答被反馈复位并重新挂起，回话随每轮发布的新版本重启。</summary>
    [Fact]
    public void Input_node_in_loop_resets_and_reparks_each_round()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.InputLoopFlow);
        environment.Executor.Output = run => $"回复{run.Context.ExecutionCount}";

        TaskId id = environment.Submit("目标");
        environment.Wait(id, _ => environment.Awaiting(id, AwaitKind.Answer).Count > 0);

        environment.Waits.Answer(id, "整体/收话", "第一问").ThrowIfError();
        _ = environment.Wait(id, snapshot =>
            environment.Executor.Started.Any(run => run.Context.Node.Name.Value == "回话" && run.Context.ExecutionCount == 1 && run.State == RunState.Succeeded)
            && environment.Awaiting(id, AwaitKind.Answer).Count > 0);

        environment.Waits.Answer(id, "整体/收话", "第二问").ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Running, done.State);
        Assert.Equal([1, 2], [.. environment.Executor.Started
            .Where(run => run.Context.Node.Name.Value == "回话")
            .Select(run => run.Context.ExecutionCount)]);
    }

    [Fact]
    public void Empty_answer_is_rejected_without_consuming_waiting()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.InputFlow);

        TaskId id = environment.Submit("补齐 README");
        WaitForInputs(environment, id, 1);

        ErrorOr<Success> empty = environment.Waits.Answer(id, null, "   ");
        Assert.True(empty.IsError);
        Assert.Contains(empty.ErrorsOrEmptyList, error => error.Description.Contains("回答内容不能为空"));

        TaskSnapshot still = environment.Snapshot(id);
        Assert.Equal(TaskState.Running, still.State);
        Assert.Single(environment.Awaiting(id, AwaitKind.Answer));
    }

    [Fact]
    public void Input_node_carries_no_model_definition()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.InputFlow);

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot parked = WaitForInputs(environment, id, 1);

        ExecutableNode input = parked.Graph.ExecutableNodes.Single(node => node.Name.Value == "用户输入");
        ExecutableNode implement = parked.Graph.ExecutableNodes.Single(node => node.Name.Value == "实施");

        Assert.Null(input.Model);
        Assert.NotNull(implement.Model);
    }

    [Fact]
    public void Answer_releases_auto_container_downstream()
    {
        using TestEnvironment environment = TestEnvironment.Create(InputInAutoContainerFlow);

        TaskId id = environment.Submit("补齐 README");
        WaitForInputs(environment, id, 1);

        environment.Waits.Answer(id, null, "需要支持多语言").ThrowIfError();

        TaskSnapshot done = environment.Settle(id);
        Assert.Equal(TaskState.Done, done.State);
        ExecutableSnapshot implement = Assert.Single(done.Executables, entry => entry.Executable.Name.Value == "实施");
        Assert.Single(implement.Runs);
    }

    [Fact]
    public void Answer_then_approval_releases_task()
    {
        using TestEnvironment environment = TestEnvironment.Create(InputInReviewContainerFlow);

        TaskId id = environment.Submit("补齐 README");
        WaitForInputs(environment, id, 1);

        environment.Waits.Answer(id, null, "需要支持多语言").ThrowIfError();

        TaskSnapshot awaiting = environment.Settle(id);
        Assert.Equal(TaskState.Running, awaiting.State);
        Assert.Equal(NodeState.Done, Assert.Single(awaiting.Containers, container => container.Path.Name == new NodeName("审查容器")).State);
        Assert.Single(environment.Awaiting(id, AwaitKind.Approve));

        environment.Waits.Approve(id).ThrowIfError();

        TaskSnapshot done = environment.Settle(id);
        Assert.Equal(TaskState.Done, done.State);
        Assert.Empty(environment.Awaiting(id, AwaitKind.Approve));
    }

    [Fact]
    public void Cancel_while_awaiting_input_ends_task()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.TwoInputFlow);

        TaskId id = environment.Submit("目标");
        WaitForInputs(environment, id, 2);

        environment.Tasks.StopTask(id).ThrowIfError();

        TaskSnapshot canceled = environment.Wait(id, snapshot => snapshot.State == TaskState.Canceled);
        Assert.Equal(TaskState.Canceled, canceled.State);
    }

    [Fact]
    public void Answer_after_cancel_fails()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.TwoInputFlow);

        TaskId id = environment.Submit("目标");
        WaitForInputs(environment, id, 2);

        environment.Tasks.StopTask(id).ThrowIfError();
        environment.Wait(id, snapshot => snapshot.State == TaskState.Canceled);

        ErrorOr<Success> answered = environment.Waits.Answer(id, null, "甲");
        Assert.True(answered.IsError);
        Assert.Contains(answered.ErrorsOrEmptyList, error => error.Description.Contains("没有等待回答的输入节点"));
    }

    [Fact]
    public void Container_with_parked_input_shows_awaiting_input()
    {
        using TestEnvironment environment = TestEnvironment.Create(InputInAutoContainerFlow);

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot parked = WaitForInputs(environment, id, 1);

        // 容器不表达成员的等待，只按成员活动给状态；等待由节点的待处理报告给出
        Assert.All(parked.Containers, container => Assert.Equal(NodeState.Pending, container.State));
        Assert.Single(environment.Awaiting(id, AwaitKind.Answer));
    }

    [Fact]
    public void Parallel_review_and_input_waits_coexist()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.ParallelInputAndReviewFlow);

        TaskId id = environment.Submit("目标");
        TaskSnapshot parked = environment.Wait(id, _ =>
            environment.Awaiting(id, AwaitKind.Answer).Count > 0
            && environment.Awaiting(id, AwaitKind.Approve).Count > 0);

        // 两种等待在待处理报告里各自可见
        Assert.Equal(TaskState.Running, parked.State);
        Assert.Single(environment.Awaiting(id, AwaitKind.Answer));
        Assert.Single(environment.Awaiting(id, AwaitKind.Approve));
    }

    /// <summary>环内输入节点每轮回答驱动一轮迭代：回话随来源新版本重跑，反馈复位后重新挂起。</summary>
    [Fact]
    public void Loop_input_node_repeats_rounds_until_limit()
    {
        using TestEnvironment environment = TestEnvironment.Create(DialogueLoopFlow);
        environment.Executor.Output = run => $"回复第{run.Context.ExecutionCount}轮";

        TaskId id = environment.Submit("对话");
        TaskSnapshot parked = WaitForInputs(environment, id, 1);
        Assert.Equal(TaskState.Running, parked.State);

        environment.Waits.Answer(id, null, "第一问").ThrowIfError();
        TaskSnapshot first = WaitForInputs(environment, id, 1);
        Assert.Equal(TaskState.Running, first.State);
        ExecutableSnapshot replyNode = first.Executables.Single(node => node.Executable.Name.Value == "回话");
        RunSnapshot firstRun = Assert.Single(replyNode.Runs);
        Assert.Contains(firstRun.Context.Seed, message => message.Text.Contains("第一问"));
        Assert.Equal("回复第1轮", firstRun.Result);
        // 本轮回答已被回话消费并随反馈复位，等待下一次输入
        Assert.Single(environment.Awaiting(id, AwaitKind.Answer));

        environment.Waits.Answer(id, null, "第二问").ThrowIfError();
        TaskSnapshot second = WaitForInputs(environment, id, 1);
        Assert.Equal(TaskState.Running, second.State);
        ExecutableSnapshot replyNodeSecond = second.Executables.Single(node => node.Executable.Name.Value == "回话");
        Assert.Equal(2, replyNodeSecond.Runs.Count);
        RunSnapshot secondRun = replyNodeSecond.Runs[^1];
        Assert.Contains(secondRun.Context.Seed, message => message.Text.Contains("第二问"));
        Assert.Equal("回复第2轮", secondRun.Result);
    }

    /// <summary>回话 run 在跑时收话已回答，此时再回答按没有等待回答的输入节点拒绝，收口后回到挂起。</summary>
    [Fact]
    public void Answer_while_loop_replying_fails_as_no_awaiting_input()
    {
        using TestEnvironment environment = TestEnvironment.Create(DialogueLoopFlow);
        environment.Executor.DelayMs = 300;

        TaskId id = environment.Submit("对话");
        WaitForInputs(environment, id, 1);

        environment.Waits.Answer(id, null, "第一问").ThrowIfError();
        environment.Wait(id, snapshot => snapshot.LiveRuns == 1);

        ErrorOr<Success> busy = environment.Waits.Answer(id, null, "挤进来");
        Assert.True(busy.IsError);
        Assert.Contains(busy.ErrorsOrEmptyList, error => error.Description.Contains("没有等待回答的输入节点"));

        TaskSnapshot back = WaitForInputs(environment, id, 1);
        Assert.Equal(TaskState.Running, back.State);
    }

    /// <summary>输入节点包在自动容器里，回答后容器放行，下游实施消费回答。</summary>
    private const string InputInAutoContainerFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    {
                      "Name": "收集",
                      "Kind": "Container",
                      "Outputs": [{ "Name": "回答", "Ref": "用户输入@回答" }],
                      "Nodes": [
                        { "Name": "用户输入", "Kind": "Input", "Question": "请补充背景", "Outputs": [{ "Name": "回答", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] }
                      ]
                    },
                    { "Name": "实施", "Kind": "Llm", "Model": "执行者", "From": [ { "回答": "收集@回答" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>输入节点包在容器里，回答后容器产出经审批节点停在待批准，批准后放行。</summary>
    private const string InputInReviewContainerFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    {
                      "Name": "审查容器",
                      "Kind": "Container",
                      "Outputs": [{ "Name": "回答", "Ref": "用户输入@回答" }],
                      "Nodes": [
                        { "Name": "用户输入", "Kind": "Input", "Question": "请补充背景", "Outputs": [{ "Name": "回答", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] }
                      ]
                    },
                    { "Name": "审阅", "Kind": "Approval", "Inputs": [{ "Name": "回答" }], "From": [ { "回答": "审查容器@回答" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>对话环：输入节点与回话互引构成环，输入节点是挂点，一轮回答驱动一轮迭代后回到挂起。</summary>
    private const string DialogueLoopFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "收话", "Kind": "Input", "Outputs": [{ "Name": "回答", "Type": "Text" }], "From": [ { "目标": "Start@目标", "回复": "回话@回复" } ] },
                    { "Name": "回话", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "回复", "Type": "Text" }], "From": [ { "回答": "收话@回答" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;
}

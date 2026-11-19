using ErrorOr;
using Kuroe.Flow.Graph.Shared.Model;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Graph.Shared.StaticGraph.Graph;
using Kuroe.Tasks.Presentation.Shared;
using Kuroe.Tasks.Shared;
using Kuroe.Pending;
using Xunit;

namespace Kuroe.Integration.Tests.Runtime.Nodes;

/// <summary>输入节点：任务停在待输入、回答即产出放行下游、回答进入下游上下文。</summary>
public sealed class InputNodeTests
{
    /// <summary>等待全部输入节点停驻后返回快照。</summary>
    private static TaskSnapshot WaitForInputs(TestEnvironment environment, TaskId id, int count)
    {
        environment.Wait(id, _ => environment.Awaiting(id, PendingKind.Answer).Count == count);

        return environment.Snapshot(id);
    }

    [Fact(DisplayName = "输入节点停到回答后释放下游")]
    public void Input_node_parks_until_answered_then_releases_downstream()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.InputFlow);

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot parked = WaitForInputs(environment, id, 1);

        Assert.Equal(TaskState.Running, parked.State);
        Assert.Single(environment.Awaiting(id, PendingKind.Answer));

        environment.Pendings.Answer(id, null, "需要支持多语言").ThrowIfError();

        TaskSnapshot done = environment.Settle(id);
        Assert.Equal(TaskState.Done, done.State);
        Assert.Empty(environment.Awaiting(id, PendingKind.Answer));

        ExecutionSnapshot implement = Assert.Single(environment.GraphExecutions(done, "实施"));
        Assert.Contains(implement.Input.Inputs,
            value => value.Text.Contains("需要支持多语言")
                && value.Origin is InputSource);
    }

    [Fact(DisplayName = "没有待处理输入时回答失败")]
    public void Answer_without_pending_input_fails()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.InputFlow);

        TaskId id = environment.Submit("补齐 README");
        WaitForInputs(environment, id, 1);

        environment.Pendings.Answer(id, null, "x").ThrowIfError();
        environment.Settle(id);

        ErrorOr<Success> answered = environment.Pendings.Answer(id, null, "又");
        Assert.True(answered.IsError);
        Assert.Contains(answered.ErrorsOrEmptyList, error => error.Description.Contains("没有等待回答的输入节点"));
    }

    [Fact(DisplayName = "两个待输入节点按图先根序列出，回答必须点名；逐个回答后任务收口")]
    public void Two_input_nodes_require_node_path()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.TwoInputFlow);

        TaskId id = environment.Submit("目标");
        WaitForInputs(environment, id, 2);

        List<PendingNode> pending = environment.Pendings.Pending(id).ThrowIfError();
        Assert.Equal(["整体/输入一", "整体/输入二"], [.. pending.Select(node => node.Step.Value)]);
        Assert.All(pending, node => Assert.Equal(PendingKind.Answer, node.Kind));

        ErrorOr<Success> ambiguous = environment.Pendings.Answer(id, null, "x");
        Assert.True(ambiguous.IsError);
        Assert.Contains(ambiguous.ErrorsOrEmptyList, error => error.Description.Contains("有 2 个输入节点等待回答"));

        environment.Pendings.Answer(id, "整体/输入一", "甲").ThrowIfError();
        TaskSnapshot stepped = WaitForInputs(environment, id, 1);
        Assert.Equal(TaskState.Running, stepped.State);

        environment.Pendings.Answer(id, "整体/输入二", "乙").ThrowIfError();
        TaskSnapshot done = environment.Settle(id);
        Assert.Equal(TaskState.Done, done.State);
    }

    [Fact(DisplayName = "有上游的输入节点提交即停驻等待回答，上游发布后作答放行下游")]
    public void Input_node_with_upstream_answers_after_upstream_releases()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.InputFromUpstreamFlow);

        TaskId id = environment.Submit("目标");
        _ = environment.Wait(id, snapshot =>
            environment.Runner.Started.Any(execution => execution.Input.Node.Label.Value == "准备" && execution.State == ExecutionState.Succeeded)
            && environment.Awaiting(id, PendingKind.Answer).Count > 0);
        TaskSnapshot parked = environment.Snapshot(id);

        Assert.Equal(TaskState.Running, parked.State);

        environment.Pendings.Answer(id, "整体/用户输入", "回答").ThrowIfError();
        TaskSnapshot done = environment.Settle(id);
        Assert.Equal(TaskState.Done, done.State);
    }

    [Fact(DisplayName = "挂点环中输入节点每轮回答被反馈复位并重新挂起，回话随每轮发布的新版本重启")]
    public void Input_node_in_loop_resets_and_reparks_each_round()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.InputLoopFlow);
        environment.Runner.Reply = execution => $"回复{execution.Input.ExecutionCount}";

        TaskId id = environment.Submit("目标");
        environment.Wait(id, _ => environment.Awaiting(id, PendingKind.Answer).Count > 0);

        environment.Pendings.Answer(id, "整体/收话", "第一问").ThrowIfError();
        _ = environment.Wait(id, snapshot =>
            environment.Runner.Started.Any(execution => execution.Input.Node.Label.Value == "回话" && execution.Input.ExecutionCount == 1 && execution.State == ExecutionState.Succeeded)
            && environment.Awaiting(id, PendingKind.Answer).Count > 0);

        environment.Pendings.Answer(id, "整体/收话", "第二问").ThrowIfError();
        TaskSnapshot done = environment.Wait(id, _ =>
            environment.Awaiting(id, PendingKind.Answer).Count == 1
            && environment.Runner.Started.Count(execution =>
                execution.Input.Node.Label.Value == "回话" && execution.State == ExecutionState.Succeeded) == 2);

        Assert.Equal(TaskState.Running, done.State);
        Assert.Equal([1, 2], [.. environment.Runner.Started
            .Where(execution => execution.Input.Node.Label.Value == "回话")
            .Select(execution => execution.Input.ExecutionCount)]);
    }

    [Fact(DisplayName = "空回答被拒绝且不消耗等待")]
    public void Empty_answer_is_rejected_without_consuming_waiting()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.InputFlow);

        TaskId id = environment.Submit("补齐 README");
        WaitForInputs(environment, id, 1);

        ErrorOr<Success> empty = environment.Pendings.Answer(id, null, "   ");
        Assert.True(empty.IsError);
        Assert.Contains(empty.ErrorsOrEmptyList, error => error.Description.Contains("回答内容不能为空"));

        TaskSnapshot still = environment.Snapshot(id);
        Assert.Equal(TaskState.Running, still.State);
        Assert.Single(environment.Awaiting(id, PendingKind.Answer));
    }

    [Fact(DisplayName = "输入节点不携带模型定义")]
    public void Input_node_carries_no_model_definition()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.InputFlow);

        TaskId id = environment.Submit("补齐 README");
        WaitForInputs(environment, id, 1);
        NodeGraph graph = environment.Flows.FindCompiled(new FlowName("默认")).ThrowIfError().Graph;

        ExecutableNode input = graph.ExecutableNodes.Single(node => node.Label.Value == "用户输入");
        ExecutableNode implement = graph.ExecutableNodes.Single(node => node.Label.Value == "实施");

        Assert.Null(input.InputBindings);
        Assert.NotNull(implement.InputBindings);
    }

    [Fact(DisplayName = "回答释放自动容器的下游")]
    public void Answer_releases_auto_container_downstream()
    {
        using TestEnvironment environment = TestEnvironment.Create(InputInAutoContainerFlow);

        TaskId id = environment.Submit("补齐 README");
        WaitForInputs(environment, id, 1);

        environment.Pendings.Answer(id, null, "需要支持多语言").ThrowIfError();

        TaskSnapshot done = environment.Settle(id);
        Assert.Equal(TaskState.Done, done.State);
        FlowStepView implement = Assert.Single(done.View.Steps, step => step.Label == "实施");
        Assert.Single(implement.Executions);
        FlowStepView input = Assert.Single(done.View.Steps, step => step.Label == "用户输入");
        Assert.Equal(1, input.ExecutionCount);
        Assert.Equal(int.MaxValue, input.MaxExecutions);
    }

    [Fact(DisplayName = "回答与审批后任务继续")]
    public void Answer_then_approval_releases_task()
    {
        using TestEnvironment environment = TestEnvironment.Create(InputInReviewContainerFlow);

        TaskId id = environment.Submit("补齐 README");
        WaitForInputs(environment, id, 1);

        environment.Pendings.Answer(id, null, "需要支持多语言").ThrowIfError();

        TaskSnapshot awaiting = environment.Wait(id, snapshot =>
            environment.Awaiting(id, PendingKind.Approve).Count == 1
            && snapshot.View.Groups.Single(group => group.Label == "审查容器").State == FlowItemState.Done);
        Assert.Equal(TaskState.Running, awaiting.State);
        Assert.Equal(FlowItemState.Done, Assert.Single(awaiting.View.Groups, group => group.Label == "审查容器").State);
        Assert.Single(environment.Awaiting(id, PendingKind.Approve));

        environment.Pendings.Approve(id).ThrowIfError();

        TaskSnapshot done = environment.Wait(id, snapshot => snapshot.State == TaskState.Done);
        Assert.Equal(TaskState.Done, done.State);
        Assert.Empty(environment.Awaiting(id, PendingKind.Approve));
    }

    [Fact(DisplayName = "等待输入时取消结束任务并拒绝回答")]
    public void Cancel_while_awaiting_input_ends_task_and_rejects_answers()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.TwoInputFlow);

        TaskId id = environment.Submit("目标");
        WaitForInputs(environment, id, 2);

        environment.Tasks.StopTask(id).ThrowIfError();

        TaskSnapshot canceled = environment.Wait(id, snapshot => snapshot.State == TaskState.Terminated);
        Assert.Equal(TaskState.Terminated, canceled.State);

        ErrorOr<Success> answered = environment.Pendings.Answer(id, null, "甲");
        Assert.True(answered.IsError);
        Assert.Contains(answered.ErrorsOrEmptyList, error => error.Description.Contains("已终止，无法回答"));
    }

    [Fact(DisplayName = "容器停在输入时显示等待输入")]
    public void Container_with_parked_input_shows_awaiting_input()
    {
        using TestEnvironment environment = TestEnvironment.Create(InputInAutoContainerFlow);

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot parked = WaitForInputs(environment, id, 1);

        // 等待中的输入节点有活动执行，容器按成员活动显示运行中；等待由节点的待处理报告给出
        Assert.All(parked.View.Groups, group => Assert.Equal(FlowItemState.Running, group.State));
        Assert.Single(environment.Awaiting(id, PendingKind.Answer));
    }

    [Fact(DisplayName = "并行评审与输入等待共存")]
    public void Parallel_review_and_input_waits_coexist()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.ParallelInputAndReviewFlow);

        TaskId id = environment.Submit("目标");
        TaskSnapshot parked = environment.Wait(id, _ =>
            environment.Awaiting(id, PendingKind.Answer).Count > 0
            && environment.Awaiting(id, PendingKind.Approve).Count > 0);

        // 两种等待在待处理报告里各自可见
        Assert.Equal(TaskState.Running, parked.State);
        Assert.Single(environment.Awaiting(id, PendingKind.Answer));
        Assert.Single(environment.Awaiting(id, PendingKind.Approve));
    }

    [Fact(DisplayName = "环内输入节点每轮回答驱动一轮迭代：回话随来源新版本重跑，反馈复位后重新挂起")]
    public void Loop_input_node_repeats_rounds_until_limit()
    {
        using TestEnvironment environment = TestEnvironment.Create(DialogueLoopFlow);
        environment.Runner.Reply = execution => $"回复第{execution.Input.ExecutionCount}轮";

        TaskId id = environment.Submit("对话");
        TaskSnapshot parked = WaitForInputs(environment, id, 1);
        Assert.Equal(TaskState.Running, parked.State);

        environment.Pendings.Answer(id, null, "第一问").ThrowIfError();
        TaskSnapshot first = environment.Wait(id, snapshot =>
            snapshot.Executions.Any(execution => execution.Journal.Any(entry => entry.Text == "回复第1轮")));
        Assert.Equal(TaskState.Running, first.State);
        ExecutionSnapshot firstExecution = environment.GraphExecutions(first, "回话")[^1];
        Assert.Contains(firstExecution.Input.Texts.Values, text => text.Contains("第一问"));
        Assert.Contains(firstExecution.Journal, entry => entry.Text == "回复第1轮");
        // 本轮回答已被回话消费并随反馈复位，等待下一次输入
        environment.Wait(id, _ => environment.Awaiting(id, PendingKind.Answer).Count == 1);
        Assert.Single(environment.Awaiting(id, PendingKind.Answer));

        environment.Pendings.Answer(id, null, "第二问").ThrowIfError();
        TaskSnapshot second = environment.Wait(id, snapshot =>
            snapshot.Executions.Any(execution => execution.Journal.Any(entry => entry.Text == "回复第2轮")));
        Assert.Equal(TaskState.Running, second.State);
        Assert.Equal(2, second.View.Steps.Single(step => step.Label == "回话").Executions.Count);
        ExecutionSnapshot secondExecution = environment.GraphExecutions(second, "回话")[^1];
        Assert.Contains(secondExecution.Input.Texts.Values, text => text.Contains("第二问"));
        Assert.Contains(secondExecution.Journal, entry => entry.Text == "回复第2轮");
    }

    [Fact(DisplayName = "回话执行在跑时收话已回答，此时再回答按没有等待回答的输入节点拒绝，收口后回到挂起")]
    public void Answer_while_loop_replying_fails_as_no_awaiting_input()
    {
        using TestEnvironment environment = TestEnvironment.Create(DialogueLoopFlow);
        NodePath reply = NodePath.Of("整体", "回话");
        environment.Runner.Gate.Hold(reply);

        TaskId id = environment.Submit("对话");
        WaitForInputs(environment, id, 1);

        environment.Pendings.Answer(id, null, "第一问").ThrowIfError();
        environment.Wait(id, snapshot => snapshot.LiveExecutions == 1);

        ErrorOr<Success> busy = environment.Pendings.Answer(id, null, "挤进来");
        Assert.True(busy.IsError);
        Assert.Contains(busy.ErrorsOrEmptyList, error => error.Description.Contains("没有等待回答的输入节点"));

        environment.Runner.Gate.Open(reply);

        TaskSnapshot back = WaitForInputs(environment, id, 1);
        Assert.Equal(TaskState.Running, back.State);
    }

    /// <summary>输入节点包在自动容器里，回答后容器放行，下游实施消费回答。</summary>
    private const string InputInAutoContainerFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }],
              "Root": {
                "Name": "整体",
                "Kind": "Container",
                "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
                  {
                    "Name": "收集",
                    "Kind": "Container",
                    "Outputs": [{ "Name": "回答", "Ref": "用户输入@回答" }],
                    "Inputs": [{ "Name": "目标" }], "From": [ { "目标": "@目标" } ], "Nodes": [
                      { "Name": "用户输入", "Kind": "Input", "Parameters": { "Question": "请补充背景" }, "Outputs": [{ "Name": "回答", "Type": "Text" }], "From": [ { "目标": "@目标" } ] }
                    ]
                  },
                  { "Name": "实施", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "回答": "收集@回答" } ] }
                ]
              }
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
              "Ports": [{ "Name": "目标", "Type": "Text" }],
              "Root": {
                "Name": "整体",
                "Kind": "Container",
                "Inputs": [{ "Name": "目标" }], "From": [ { "目标": "@目标" } ], "Nodes": [
                  {
                    "Name": "审查容器",
                    "Kind": "Container",
                    "Outputs": [{ "Name": "回答", "Ref": "用户输入@回答" }],
                    "Inputs": [{ "Name": "目标" }], "From": [ { "目标": "@目标" } ], "Nodes": [
                      { "Name": "用户输入", "Kind": "Input", "Parameters": { "Question": "请补充背景" }, "Outputs": [{ "Name": "回答", "Type": "Text" }], "From": [ { "目标": "@目标" } ] }
                    ]
                  },
                  { "Name": "审阅", "Kind": "Approval", "Inputs": [{ "Name": "回答" }], "From": [ { "回答": "审查容器@回答" } ] }
                ]
              }
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
              "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }],
              "Root": {
                "Name": "整体",
                "Kind": "Container",
                "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
                  { "Name": "收话", "Kind": "Input", "Outputs": [{ "Name": "回答", "Type": "Text" }], "From": [ { "目标": "@目标", "回复": "回话@回复" } ] },
                  { "Name": "回话", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "回复", "Type": "Text" }], "From": [ { "回答": "收话@回答" } ] }
                ]
              }
            }
          ]
        }
        """;
}

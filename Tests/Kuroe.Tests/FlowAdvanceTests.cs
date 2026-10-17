using ErrorOr;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Runs;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Pending;
using Xunit;

namespace Kuroe.Tests;

/// <summary>图驱动推进：展开、汇拢、放行、返工、未收口与取消。默认流程即计划后按条目展开实施。</summary>
public sealed class FlowAdvanceTests
{
    [Fact]
    public void Empty_flow_library_rejects_submit_without_flow()
    {
        using TestEnvironment environment = TestEnvironment.Create(flowsJson: "{}", defaultFlow: null);

        ErrorOr<TaskSnapshot> submitted = environment.Tasks.Submit("补齐 README", null, null);

        Assert.True(submitted.IsError);
        Assert.Contains(submitted.ErrorsOrEmptyList,
            error => error.Description.Contains("未设置默认流程"));
    }

    [Fact]
    public void Plan_implement_completes_task()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables[0].Runs);
        Assert.Equal(2, done.Executables[1].Runs.Count);
        Assert.Equal(2, done.FrontierNodes);
    }

    [Fact]
    public void Approval_on_plan_parks_until_approved()
    {
        using TestEnvironment environment = TestEnvironment.Create(ReviewOnPlanFlow);

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot parked = environment.Settle(id);
        Assert.Equal(TaskState.Running, parked.State);
        Assert.Single(environment.Awaiting(id, PendingKind.Approve));
        Assert.Single(parked.Executables[0].Runs);
        Assert.Empty(parked.Executables[2].Runs);

        environment.Pendings.Approve(id).ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(2, done.Executables[2].Runs.Count);
    }

    [Fact]
    public void Plan_step_without_submission_blocks_task()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Executor.SubmitsPlan = false;

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot blocked = environment.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        RunSnapshot plan = Assert.Single(blocked.Executables[0].Runs);
        Assert.Equal(RunState.Failed, plan.State);
        Assert.Contains(plan.Failures, failure => failure.Contains("未收口"));
    }

    [Fact]
    public void Rework_with_unknown_item_keeps_task_blocked()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Executor.FailsWhen = run => run.Context.Node.Label.Value == "分配执行" && run.Context.ItemIndex == 0;

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot blocked = environment.Settle(id);
        Assert.Equal(TaskState.Blocked, blocked.State);

        // 指定的条目不属于阻塞目标时，返工视为未处理，任务保持阻塞
        Assert.True(environment.Tasks.Rework(id, 99).IsError);
        Assert.Equal(TaskState.Blocked, environment.Snapshot(id).State);
    }

    [Fact]
    public void Plan_uncollected_then_rework_reruns_plan()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Executor.SubmitsPlan = false;

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot blocked = environment.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Single(blocked.Executables[0].Runs);

        environment.Executor.SubmitsPlan = true;
        environment.Tasks.Rework(id, null).ThrowIfError();

        TaskSnapshot done = environment.Wait(id, snapshot => snapshot.State == TaskState.Done);
        Assert.Equal(2, done.Executables[0].Runs.Count);
    }

    [Fact]
    public void Run_failure_then_rework_reruns_the_instance()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        int failures = 0;
        environment.Executor.FailsWhen = run =>
            run.Context.Node.Label.Value == "分配执行" && Interlocked.Increment(ref failures) <= 2;

        TaskId id = environment.Submit("补齐 README");
        environment.Wait(id, snapshot => snapshot.State == TaskState.Blocked);

        environment.Tasks.Rework(id, null).ThrowIfError();
        TaskSnapshot done = environment.Wait(id, snapshot => snapshot.State == TaskState.Done);

        Assert.Equal(TaskState.Done, done.State);
        // 实施首次一轮失败，返工后第二轮重跑两个实例并正常收口
        Assert.Equal(4, done.Executables[1].Runs.Count);
    }

    [Fact]
    public void Cancel_after_block_settles_the_task()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Executor.FailsWhen = run => run.Context.Node.Label.Value == "分配执行";

        TaskId id = environment.Submit("补齐 README");
        environment.Wait(id, snapshot => snapshot.State == TaskState.Blocked);

        environment.Tasks.StopTask(id).ThrowIfError();
        TaskSnapshot canceled = environment.Settle(id);

        Assert.Equal(TaskState.Canceled, canceled.State);
        Assert.DoesNotContain(canceled.ExecutableStates, state => state.State == NodeState.Blocked);
        Assert.Equal(1, environment.Registry.ClearSettled());
    }

    [Fact]
    public void Cancel_task_stops_every_unit()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Executor.DelayMs = 300;

        TaskId id = environment.Submit("补齐 README");
        environment.Wait(id, snapshot => snapshot.LiveRuns > 0);
        environment.Tasks.StopTask(id).ThrowIfError();

        TaskSnapshot canceled = environment.Settle(id);
        Assert.Equal(TaskState.Canceled, canceled.State);
        Assert.Equal(0, canceled.LiveRuns);
    }

    [Fact]
    public void Parallel_dispatch_assigns_items_to_branches()
    {
        using TestEnvironment environment = TestEnvironment.Create(ParallelFunnelFlow);
        environment.Executor.ItemsJson = BranchedItemsJson;

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 规划各一个 run，两条分支各按归属条目派一个实施者
        Assert.Single(done.Executables[0].Runs);
        Assert.Single(done.Executables[1].Runs);
        Assert.Single(done.Executables[2].Runs);
        Assert.Equal(0, Assert.Single(done.Executables[1].Runs).Context.ItemIndex);
        Assert.Equal(1, Assert.Single(done.Executables[2].Runs).Context.ItemIndex);
    }

    [Fact]
    public void Parallel_dispatch_requires_branch_on_every_item()
    {
        using TestEnvironment environment = TestEnvironment.Create(ParallelFunnelFlow);

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot blocked = environment.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Contains(environment.Executor.Submissions, text => text.Contains("必须写明分支 Branch"));
    }

    [Fact]
    public void Parallel_dispatch_after_approval_expands_branches()
    {
        using TestEnvironment environment = TestEnvironment.Create(ParallelReviewGateFlow);
        environment.Executor.ItemsJson = BranchedItemsJson;

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot parked = environment.Settle(id);
        Assert.Equal(TaskState.Running, parked.State);
        Assert.Single(environment.Awaiting(id, PendingKind.Approve));

        environment.Pendings.Approve(id).ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 批准推进必须展开分支而不是整节点顶替：每个分支各一个实施者
        Assert.Single(done.Executables[2].Runs);
        Assert.Single(done.Executables[3].Runs);
    }

    [Fact]
    public void Parallel_single_branch_expands_after_approval()
    {
        using TestEnvironment environment = TestEnvironment.Create(ParallelSingleBranchReviewGateFlow);
        environment.Executor.ItemsJson = SingleBranchItemsJson;

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot parked = environment.Settle(id);
        Assert.Equal(TaskState.Running, parked.State);
        Assert.Single(environment.Awaiting(id, PendingKind.Approve));

        environment.Pendings.Approve(id).ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 单分支也要按拆分展开：两条目各一个实施者
        Assert.Equal(2, done.Executables[2].Runs.Count);
    }

    [Fact]
    public void Plan_dispatch_instruction_lists_available_parallel_branches()
    {
        using TestEnvironment environment = TestEnvironment.Create(ParallelFunnelFlow);
        environment.Executor.ItemsJson = BranchedItemsJson;

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 规划者的本轮接口说明里给出可用的分支名，模型据此交回带 Branch 的条目
        RunSnapshot planner = done.Executables[0].Runs[0];
        Assert.Contains("可选分支：撰写、排版", planner.Context.Briefing, StringComparison.Ordinal);
    }

    [Fact]
    public void Multiple_plan_sources_expand_and_settle_independently()
    {
        using TestEnvironment environment = TestEnvironment.Create(MultiPlanFunnelFlow);
        environment.Executor.ItemsJson = MultiWorkItemParserJson;

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 每个规划各一个 run，两个实施各按自己的拆分展开两条目
        Assert.Single(done.Executables[0].Runs);
        Assert.Single(done.Executables[1].Runs);
        Assert.Equal(2, done.Executables[2].Runs.Count);
        Assert.Equal(2, done.Executables[3].Runs.Count);
        // 条目号只在自己的规划空间里有效，两个实施各自从 0 起
        Assert.All(done.Executables[2].Runs, run => Assert.InRange(run.Context.ItemIndex!.Value, 0, 1));
        Assert.All(done.Executables[3].Runs, run => Assert.InRange(run.Context.ItemIndex!.Value, 0, 1));
    }

    [Fact]
    public void Approve_one_approval_keeps_the_other_branch_waiting()
    {
        using TestEnvironment environment = TestEnvironment.Create(ParallelApprovalFlow);
        environment.Executor.ItemsJson = BranchedItemsJson;

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot parked = environment.Settle(id);
        Assert.Equal(TaskState.Running, parked.State);
        Assert.Equal(2, environment.Awaiting(id, PendingKind.Approve).Count);

        NodePath review = NodePath.Of("整体", "审阅甲");
        environment.Pendings.Approve(id, [review]).ThrowIfError();

        // 只放行甲分支的审批节点，乙分支仍停在待批准，汇总不启动
        TaskSnapshot partial = environment.Settle(id);
        Assert.Equal(TaskState.Running, partial.State);
        Assert.Single(environment.Awaiting(id, PendingKind.Approve));
    }

    private const string BranchedItemsJson = """
        [
          { "Title": "甲", "Instruction": "做甲", "Acceptance": "甲可见", "Branch": "撰写" },
          { "Title": "乙", "Instruction": "做乙", "Acceptance": "乙可见", "Branch": "排版" }
        ]
        """;

    private const string SingleBranchItemsJson = """
        [
          { "Title": "甲", "Instruction": "做甲", "Acceptance": "甲可见", "Branch": "撰写" },
          { "Title": "乙", "Instruction": "做乙", "Acceptance": "乙可见", "Branch": "撰写" }
        ]
        """;

    private const string ReviewOnPlanFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                { "Name": "制定计划", "Kind": "Llm", "Model": "规划者", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] },
                { "Name": "审阅", "Kind": "Approval", "Question": "确认计划", "Inputs": [{ "Name": "计划", "Type": "Signal" }], "From": [ { "计划": "制定计划@拆分" } ] },
                { "Name": "分配执行", "Kind": "Llm", "Model": "执行者", "Tools": ["GetLocalTime"], "Mode": "PerItem", "Inputs": [{ "Name": "任务" }, { "Name": "批准", "Type": "Signal" }], "From": [ { "任务": "制定计划@拆分", "批准": "审阅@计划" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private const string ParallelFunnelFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "实施者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                { "Name": "制定计划", "Kind": "Llm", "Model": "规划者", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] },
                { "Name": "撰写", "Kind": "Llm", "Model": "实施者", "Mode": "PerItem", "Branch": "撰写", "From": [ { "拆分": "制定计划@拆分" } ] },
                { "Name": "排版", "Kind": "Llm", "Model": "实施者", "Mode": "PerItem", "Branch": "排版", "From": [ { "拆分": "制定计划@拆分" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private const string ParallelReviewGateFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "实施者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                { "Name": "制定计划", "Kind": "Llm", "Model": "规划者", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] },
                { "Name": "审阅", "Kind": "Approval", "Inputs": [{ "Name": "计划", "Type": "Signal" }], "From": [ { "计划": "制定计划@拆分" } ] },
                { "Name": "撰写", "Kind": "Llm", "Model": "实施者", "Mode": "PerItem", "Branch": "撰写", "Inputs": [{ "Name": "任务" }, { "Name": "批准", "Type": "Signal" }], "From": [ { "任务": "制定计划@拆分", "批准": "审阅@计划" } ] },
                { "Name": "排版", "Kind": "Llm", "Model": "实施者", "Mode": "PerItem", "Branch": "排版", "Inputs": [{ "Name": "任务" }, { "Name": "批准", "Type": "Signal" }], "From": [ { "任务": "制定计划@拆分", "批准": "审阅@计划" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private const string ParallelSingleBranchReviewGateFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "实施者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                { "Name": "制定计划", "Kind": "Llm", "Model": "规划者", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] },
                { "Name": "审阅", "Kind": "Approval", "Inputs": [{ "Name": "计划", "Type": "Signal" }], "From": [ { "计划": "制定计划@拆分" } ] },
                { "Name": "撰写", "Kind": "Llm", "Model": "实施者", "Mode": "PerItem", "Branch": "撰写", "Inputs": [{ "Name": "任务" }, { "Name": "批准", "Type": "Signal" }], "From": [ { "任务": "制定计划@拆分", "批准": "审阅@计划" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private const string ParallelApprovalFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "实施者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                { "Name": "制定计划", "Kind": "Llm", "Model": "规划者", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] },
                { "Name": "审阅甲", "Kind": "Approval", "Inputs": [{ "Name": "计划", "Type": "Signal" }], "From": [ { "计划": "制定计划@拆分" } ] },
                { "Name": "审阅乙", "Kind": "Approval", "Inputs": [{ "Name": "计划", "Type": "Signal" }], "From": [ { "计划": "制定计划@拆分" } ] },
                { "Name": "撰写", "Kind": "Llm", "Model": "实施者", "Mode": "PerItem", "Branch": "撰写", "Inputs": [{ "Name": "任务" }, { "Name": "批准", "Type": "Signal" }], "From": [ { "任务": "制定计划@拆分", "批准": "审阅甲@计划" } ] },
                { "Name": "排版", "Kind": "Llm", "Model": "实施者", "Mode": "PerItem", "Branch": "排版", "Inputs": [{ "Name": "任务" }, { "Name": "批准", "Type": "Signal" }], "From": [ { "任务": "制定计划@拆分", "批准": "审阅乙@计划" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private const string MultiPlanFunnelFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "实施者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                { "Name": "制定A计划", "Kind": "Llm", "Model": "规划者", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] },
                { "Name": "制定B计划", "Kind": "Llm", "Model": "规划者", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] },
                { "Name": "实施A", "Kind": "Llm", "Model": "实施者", "Mode": "PerItem", "From": [ { "拆分": "制定A计划@拆分" } ] },
                { "Name": "实施B", "Kind": "Llm", "Model": "实施者", "Mode": "PerItem", "From": [ { "拆分": "制定B计划@拆分" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private const string MultiWorkItemParserJson = """
        [
          { "Title": "甲", "Instruction": "做甲", "Acceptance": "甲可见" },
          { "Title": "乙", "Instruction": "做乙", "Acceptance": "乙可见" }
        ]
        """;
}

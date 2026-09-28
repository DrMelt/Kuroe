using System.Threading;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Tests;

/// <summary>图驱动推进：展开、汇拢、放行、返工、未收口与取消。默认流程即计划后按条目展开并整体检查。</summary>
public sealed class FlowAdvanceTests
{
    [Fact]
    public void Plan_implement_funnel_check_completes_task()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Nodes[0].Runs);
        Assert.Equal(2, done.Nodes[1].Runs.Count);
        // 整体检查由单个 run 汇拢全部条目实施
        Assert.Single(done.Nodes[2].Runs);
        Assert.Equal(3, done.FrontierNodes);
        // 整体检查结论按覆盖的拆分回填到条目
        Assert.Equal(2, done.ItemStates.Count);
        Assert.All(done.ItemStates, state => Assert.Equal(3, state.ExecutableIndex));
        Assert.All(done.ItemStates, state => Assert.Equal(UnitVerdict.Verified, state.Verdict));
    }

    [Fact]
    public void Rejected_funnel_check_retries_all_items_with_findings()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.CheckPasses = run => run.Context.ExecutionCount > 1;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);

        // 两条目各实施两轮，整体检查两轮
        Assert.Equal(4, done.Nodes[1].Runs.Count);
        Assert.Equal(2, done.Nodes[2].Runs.Count);
        Assert.All(done.ItemStates, state => Assert.Equal(UnitVerdict.Verified, state.Verdict));

        RunSnapshot retry = Assert.Single(done.Nodes[1].Runs,
            run => run.Context.ItemIndex == 0 && run.Context.ExecutionCount == 2);
        Assert.Contains(retry.Context.Seed, message => message.Text.Contains("上一轮检查未通过"));
    }

    [Fact]
    public void Funnel_check_without_findings_is_rejected_and_blocks()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.CheckPasses = _ => false;
        harness.Executor.FindingsForCheck = string.Empty;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Contains(harness.Executor.Submissions, text => text.Contains("被拒绝：不通过时要列出问题"));
    }

    [Fact]
    public void Review_gate_parks_plan_until_approved()
    {
        using KuroeHarness harness = KuroeHarness.Create(ReviewOnPlanFlow);

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot parked = harness.Settle(id);
        Assert.Equal(TaskState.AwaitingApproval, parked.State);
        Assert.Single(parked.Nodes[0].Runs);

        harness.Tasks.Approve(id).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(2, done.Nodes[1].Runs.Count);
    }

    [Fact]
    public void Per_item_check_flow_checks_each_item_separately()
    {
        using KuroeHarness harness = KuroeHarness.Create(PerItemCheckFlow);

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 逐条检查：每个条目各一个检查 run
        Assert.Equal(2, done.Nodes[2].Runs.Count);
        Assert.All(done.ItemStates, state => Assert.Equal(UnitVerdict.Verified, state.Verdict));
    }

    [Fact]
    public void Plan_step_without_submission_blocks_task()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.SubmitsPlan = false;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        RunSnapshot plan = Assert.Single(blocked.Nodes[0].Runs);
        Assert.Equal(RunState.Failed, plan.State);
        Assert.Contains(plan.Failures, failure => failure.Contains("未收口"));
    }

    [Fact]
    public void Rework_with_unknown_item_keeps_task_blocked()
    {
        using KuroeHarness harness = KuroeHarness.Create(StopOnRejectFlow);
        harness.Executor.CheckPasses = _ => false;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);
        Assert.Equal(TaskState.Blocked, blocked.State);

        // 指定的条目不属于阻塞目标时，返工视为未处理，任务保持阻塞
        Assert.True(harness.Tasks.Rework(id, 99).IsError);
        Assert.Equal(TaskState.Blocked, harness.Snapshot(id).State);
    }

    [Fact]
    public void Partial_rework_keeps_other_failed_items_blocked()
    {
        using KuroeHarness harness = KuroeHarness.Create(PerItemCheckFlow);
        // 检查到第三轮才开始通过：前两轮两条目都失败并阻塞
        harness.Executor.CheckPasses = run => run.Context.NodeIndex == 2 && run.Context.ExecutionCount >= 3;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);
        Assert.Equal(TaskState.Blocked, blocked.State);
        // 两条目各两轮检查均被拒，检查节点停在等返工
        Assert.Equal(4, blocked.Nodes[2].Runs.Count);

        // 只返工条目 0：条目 1 的返工目标保留，只重跑条目 0 的实施
        harness.Tasks.Rework(id, 0).ThrowIfError();
        harness.Wait(id,
            s => s.Nodes[1].Runs.Any(run => run.Context.ItemIndex == 0 && run.Context.ExecutionCount == 3));
        TaskSnapshot partial = harness.Settle(id);
        // 条目 1 的返工目标保留，检查节点保持阻塞不重开
        Assert.Equal(TaskState.Blocked, partial.State);
        Assert.Equal(4, partial.Nodes[2].Runs.Count);
        Assert.Contains(partial.Nodes[1].Runs, run => run.Context.ItemIndex == 0 && run.Context.ExecutionCount == 3);
        Assert.DoesNotContain(partial.Nodes[1].Runs, run => run.Context.ItemIndex == 1 && run.Context.ExecutionCount == 3);

        // 返工条目 1：目标清空后检查重开，第三轮检查两条目通过
        harness.Tasks.Rework(id, 1).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);
        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(6, done.Nodes[2].Runs.Count);
        Assert.All(done.ItemStates, state => Assert.Equal(UnitVerdict.Verified, state.Verdict));
    }

    [Fact]
    public void Per_item_checks_from_separate_spaces_keep_their_own_verdicts()
    {
        using KuroeHarness harness = KuroeHarness.Create(TwoPlanPerItemCheckFlow);
        harness.Executor.CheckPasses = run => run.Context.NodeIndex != 2 || run.Context.ItemIndex != 0;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);
        Assert.Equal(TaskState.Blocked, blocked.State);

        ItemStateSnapshot[] fromFirst = [.. blocked.ItemStates.Where(state => state.ExecutableIndex == 2)];
        ItemStateSnapshot[] fromSecond = [.. blocked.ItemStates.Where(state => state.ExecutableIndex == 5)];

        // 第一个检查空间的条目 0 被拒，条目 1 通过；结论按检查节点归属不跨空间串号
        Assert.Equal(2, fromFirst.Length);
        Assert.Equal(UnitVerdict.Rejected, Assert.Single(fromFirst, state => state.ItemIndex == 0).Verdict);
        Assert.Equal(UnitVerdict.Verified, Assert.Single(fromFirst, state => state.ItemIndex == 1).Verdict);

        // 第二个检查空间不受前一空间拒绝影响，两个条目都通过
        Assert.Equal(2, fromSecond.Length);
        Assert.All(fromSecond, state => Assert.Equal(UnitVerdict.Verified, state.Verdict));
    }

    [Fact]
    public void Stop_reject_action_parks_units_until_rework()
    {
        using KuroeHarness harness = KuroeHarness.Create(StopOnRejectFlow);
        harness.Executor.CheckPasses = _ => false;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);
        Assert.Equal(TaskState.Blocked, blocked.State);

        harness.Tasks.Rework(id, null).ThrowIfError();
        // 返工退回实施节点，重跑一轮实施后再次整体检查并停留
        TaskSnapshot reopened = harness.Wait(id, snapshot =>
            snapshot.Nodes[1].Runs.Any(run => run.Context.ItemIndex == 0 && run.Context.ExecutionCount > 1));

        Assert.Equal(2, reopened.Nodes[1].Runs.Count(run => run.Context.ItemIndex == 0));
    }

    [Fact]
    public void Rework_after_uncollected_review_restarts_review()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.SubmitsVerdict = false;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Single(blocked.Nodes[2].Runs);

        harness.Executor.SubmitsVerdict = true;
        harness.Tasks.Rework(id, null).ThrowIfError();

        TaskSnapshot done = harness.Wait(id, snapshot => snapshot.State == TaskState.Done);
        // 检查未收口后重跑自身并正常收口，实施不随返工重跑
        Assert.Equal(2, done.Nodes[1].Runs.Count);
        Assert.Equal(2, done.Nodes[2].Runs.Count);
    }

    [Fact]
    public void Plan_uncollected_then_rework_reruns_plan()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.SubmitsPlan = false;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Single(blocked.Nodes[0].Runs);

        harness.Executor.SubmitsPlan = true;
        harness.Tasks.Rework(id, null).ThrowIfError();

        TaskSnapshot done = harness.Wait(id, snapshot => snapshot.State == TaskState.Done);
        Assert.Equal(2, done.Nodes[0].Runs.Count);
    }

    [Fact]
    public void Run_failure_then_rework_reruns_the_instance()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        int failures = 0;
        harness.Executor.FailsWhen = run =>
            run.Context.NodeIndex == 2 && Interlocked.Increment(ref failures) <= 2;

        TaskId id = harness.Submit("补齐 README");
        harness.Wait(id, snapshot => snapshot.State == TaskState.Blocked);

        harness.Tasks.Rework(id, null).ThrowIfError();
        TaskSnapshot done = harness.Wait(id, snapshot => snapshot.State == TaskState.Done);

        Assert.Equal(TaskState.Done, done.State);
        // 实施首次一轮失败，返工后第二轮重跑两个实例并正常收口
        Assert.Equal(4, done.Nodes[1].Runs.Count);
        Assert.Single(done.Nodes[2].Runs);
    }

    [Fact]
    public void Cancel_after_block_settles_the_task()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.CheckPasses = _ => false;

        TaskId id = harness.Submit("补齐 README");
        harness.Wait(id, snapshot => snapshot.State == TaskState.Blocked);

        harness.Tasks.StopTask(id).ThrowIfError();
        TaskSnapshot canceled = harness.Settle(id);

        Assert.Equal(TaskState.Canceled, canceled.State);
        Assert.DoesNotContain(canceled.NodeStates,
            state => state.State is NodeState.Blocked or NodeState.AwaitingApproval);
        Assert.Equal(1, harness.Registry.ClearFinished());
    }

    [Fact]
    public void Cancel_task_stops_every_unit()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.DelayMs = 300;

        TaskId id = harness.Submit("补齐 README");
        harness.Wait(id, snapshot => snapshot.LiveRuns > 0);
        harness.Tasks.StopTask(id).ThrowIfError();

        TaskSnapshot canceled = harness.Settle(id);
        Assert.Equal(TaskState.Canceled, canceled.State);
        Assert.Equal(0, canceled.LiveRuns);
    }

    [Fact]
    public void Parallel_dispatch_assigns_items_to_branches_and_funnels()
    {
        using KuroeHarness harness = KuroeHarness.Create(ParallelFunnelFlow);
        harness.Executor.ItemsJson = BranchedItemsJson;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 规划各一个 run，两个分支各一个实施者，整体检查一个
        Assert.Single(done.Nodes[0].Runs);
        Assert.Single(done.Nodes[1].Runs);
        Assert.Single(done.Nodes[2].Runs);
        Assert.Single(done.Nodes[3].Runs);
        Assert.Single(done.Nodes[1].Runs, run => run.Context.ItemIndex == 0);
        Assert.Single(done.Nodes[2].Runs, run => run.Context.ItemIndex == 1);
    }

    [Fact]
    public void Parallel_funnel_reject_returns_each_item_to_its_branch()
    {
        using KuroeHarness harness = KuroeHarness.Create(ParallelFunnelFlow);
        harness.Executor.ItemsJson = BranchedItemsJson;
        harness.Executor.CheckPasses = run => run.Context.ExecutionCount > 1;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 两个分支的实施各两轮，整体检查两轮
        Assert.Equal(2, done.Nodes[1].Runs.Count);
        Assert.Equal(2, done.Nodes[2].Runs.Count);
        Assert.Equal(2, done.Nodes[3].Runs.Count);

        // 退回后的实施上下文带上整体检查意见
        RunSnapshot retry = Assert.Single(done.Nodes[1].Runs, run => run.Context.ExecutionCount == 2);
        Assert.Contains(retry.Context.Seed, message => message.Text.Contains("上一轮检查未通过"));
    }

    [Fact]
    public void Parallel_dispatch_requires_branch_on_every_item()
    {
        using KuroeHarness harness = KuroeHarness.Create(ParallelFunnelFlow);

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Contains(harness.Executor.Submissions, text => text.Contains("必须写明分支 Branch"));
    }

    [Fact]
    public void Parallel_per_item_check_reviews_each_item_separately()
    {
        using KuroeHarness harness = KuroeHarness.Create(ParallelPerItemCheckFlow);
        harness.Executor.ItemsJson = BranchedItemsJson;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 逐条检查：每个条目各一个检查 run
        Assert.Equal(2, done.Nodes[3].Runs.Count);
        Assert.All(done.ItemStates, state => Assert.Equal(UnitVerdict.Verified, state.Verdict));
    }

    [Fact]
    public void Parallel_dispatch_after_approval_expands_branches()
    {
        using KuroeHarness harness = KuroeHarness.Create(ParallelReviewGateFlow);
        harness.Executor.ItemsJson = BranchedItemsJson;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot parked = harness.Settle(id);
        Assert.Equal(TaskState.AwaitingApproval, parked.State);

        harness.Tasks.Approve(id).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 批准推进必须展开分支而不是整节点顶替：每个分支各一个实施者、整体检查一个
        Assert.Single(done.Nodes[1].Runs);
        Assert.Single(done.Nodes[2].Runs);
        Assert.Single(done.Nodes[3].Runs);
    }

    [Fact]
    public void Parallel_single_branch_expands_after_approval()
    {
        using KuroeHarness harness = KuroeHarness.Create(ParallelSingleBranchReviewGateFlow);
        harness.Executor.ItemsJson = SingleBranchItemsJson;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot parked = harness.Settle(id);
        Assert.Equal(TaskState.AwaitingApproval, parked.State);

        harness.Tasks.Approve(id).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 单分支也要按拆分展开：两条目各一个实施者
        Assert.Equal(2, done.Nodes[1].Runs.Count);
        Assert.Single(done.Nodes[2].Runs);
    }

    [Fact]
    public void Plan_dispatch_instruction_lists_available_parallel_branches()
    {
        using KuroeHarness harness = KuroeHarness.Create(ParallelFunnelFlow);
        harness.Executor.ItemsJson = BranchedItemsJson;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 规划者的上下文里给出可用的分支名，模型据此交回带 Branch 的条目
        RunSnapshot planner = done.Nodes[0].Runs[0];
        Assert.Contains("可选分支：撰写、排版", planner.Context.Instruction);
    }

    [Fact]
    public void Static_split_expands_without_plan_run()
    {
        using KuroeHarness harness = KuroeHarness.Create(StaticSplitFlow);

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 静态拆分不派规划 run，两条目各一个实施者，整体检查一个
        Assert.Empty(done.Nodes[0].Runs);
        Assert.Equal(2, done.Nodes[1].Runs.Count);
        Assert.Single(done.Nodes[2].Runs);
        Assert.All(done.ItemStates, state => Assert.Equal(UnitVerdict.Verified, state.Verdict));

        RunSnapshot first = done.Nodes[1].Runs[0];
        Assert.Contains(first.Context.Seed, message => message.Text.Contains("本条目：甲\n要做：做甲\n验收标准：甲可见"));
    }

    [Fact]
    public void Static_split_with_model_extras_merges_fixed_and_proposed()
    {
        using KuroeHarness harness = KuroeHarness.Create(ExtrasSplitFlow);

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 规划 run 跑一次交补充条目，固定条目加两条补充共三个实施者
        Assert.Single(done.Nodes[0].Runs);
        Assert.Equal(3, done.Nodes[1].Runs.Count);

        // 规划指令给出固定条目参考与补充上限
        RunSnapshot plan = done.Nodes[0].Runs[0];
        Assert.Contains("已按标准固定 1 条", plan.Context.Instruction);
        Assert.Contains("补充至多 2 条", plan.Context.Instruction);
        Assert.Contains("验收统一为", plan.Context.Instruction);

        // 固定条目保留配置内容，补充条目接受模型文本并注入统一验收
        RunSnapshot fixedRun = Assert.Single(done.Nodes[1].Runs,
            run => run.Context.Seed.Any(message => message.Text.Contains("本条目：固定任务")));
        Assert.Contains(fixedRun.Context.Seed, message => message.Text.Contains("验收标准：固定验收"));

        RunSnapshot extraRun = Assert.Single(done.Nodes[1].Runs,
            run => run.Context.Seed.Any(message => message.Text.Contains("本条目：甲")));
        Assert.Contains(extraRun.Context.Seed, message => message.Text.Contains("验收标准：统一验收"));
    }

    [Fact]
    public void Extras_over_limit_rejects_submission_and_blocks()
    {
        using KuroeHarness harness = KuroeHarness.Create(ConstrainedSplitFlow);

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Contains(harness.Executor.Submissions, text => text.Contains("最多补充 1 条"));
    }

    [Fact]
    public void Static_split_dispatches_items_to_their_branches()
    {
        using KuroeHarness harness = KuroeHarness.Create(StaticParallelSplitFlow);

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 静态拆分不派规划 run，条目按声明的分支落分支各一个实施者
        Assert.Empty(done.Nodes[0].Runs);
        Assert.Single(done.Nodes[1].Runs, run => run.Context.ItemIndex == 0);
        Assert.Single(done.Nodes[2].Runs, run => run.Context.ItemIndex == 1);
        Assert.Single(done.Nodes[3].Runs);
    }

    [Fact]
    public void Multiple_plan_sources_expand_and_settle_independently()
    {
        using KuroeHarness harness = KuroeHarness.Create(MultiPlanFunnelFlow);
        harness.Executor.ItemsJson = MultiPlanItemsJson;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 每个规划各一个 run，两个实施各按自己的拆分展开两条目
        Assert.Single(done.Nodes[0].Runs);
        Assert.Single(done.Nodes[1].Runs);
        Assert.Equal(2, done.Nodes[2].Runs.Count);
        Assert.Equal(2, done.Nodes[3].Runs.Count);
        // 条目号只在自己的规划空间里有效，两个实施各自从 0 起
        Assert.All(done.Nodes[2].Runs, run => Assert.InRange(run.Context.ItemIndex!.Value, 0, 1));
        Assert.All(done.Nodes[3].Runs, run => Assert.InRange(run.Context.ItemIndex!.Value, 0, 1));
        // 整体检查汇拢两条实施来源
        Assert.Single(done.Nodes[4].Runs);
    }

    [Fact]
    public void Whole_executable_can_be_checked()
    {
        using KuroeHarness harness = KuroeHarness.Create(SingleImplementReviewFlow);

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // Single 整节点实施一个 run，被检查节点整体引用
        Assert.Single(done.Nodes[0].Runs);
        Assert.Single(done.Nodes[1].Runs);
        Assert.Equal(NodeOutput.Review, done.Nodes[1].Runs[0].Context.Output);
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
                { "Name": "规划者" },
                { "Name": "执行者" },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan", "Gate": "Review" },
                { "Name": "分配执行", "Model": "执行者", "Tools": ["GetLocalTime", "GetWeather"], "Mode": "PerItem", "From": ["制定计划"] },
                { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["制定计划", "分配执行"], "OnReject": "Retry", "MaxAttempts": 2 }
              ]
            }
          ]
        }
        """;

    private const string StopOnRejectFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者" },
                { "Name": "执行者" },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
                { "Name": "分配执行", "Model": "执行者", "Tools": ["GetLocalTime", "GetWeather"], "Mode": "PerItem", "From": ["制定计划"] },
                { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["制定计划", "分配执行"], "OnReject": "Stop" }
              ]
            }
          ]
        }
        """;

    private const string PerItemCheckFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者" },
                { "Name": "执行者" },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
                { "Name": "分配执行", "Model": "执行者", "Tools": ["GetLocalTime", "GetWeather"], "Mode": "PerItem", "From": ["制定计划"] },
                { "Name": "逐条检查", "Model": "检查者", "Output": "Review", "Mode": "PerItem", "From": ["分配执行"], "OnReject": "Retry", "MaxAttempts": 2 }
              ]
            }
          ]
        }
        """;

    private const string TwoPlanPerItemCheckFlow = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }, { "Name": "实施者" }, { "Name": "检查者" }], "Nodes": [
          { "Name": "制定A计划", "Model": "规划者", "Output": "Plan" },
          { "Name": "实施A", "Model": "实施者", "Mode": "PerItem", "From": ["制定A计划"] },
          { "Name": "检查A", "Model": "检查者", "Output": "Review", "Mode": "PerItem", "From": ["实施A"], "OnReject": "Stop" },
          { "Name": "制定B计划", "Model": "规划者", "Output": "Plan" },
          { "Name": "实施B", "Model": "实施者", "Mode": "PerItem", "From": ["制定B计划"] },
          { "Name": "检查B", "Model": "检查者", "Output": "Review", "Mode": "PerItem", "From": ["实施B"], "OnReject": "Stop" }
        ] } ] }
        """;

    private const string ParallelFunnelFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者" },
                { "Name": "实施者" },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
                { "Name": "撰写", "Model": "实施者", "Mode": "PerItem", "Branch": "撰写", "From": ["制定计划"] },
                { "Name": "排版", "Model": "实施者", "Mode": "PerItem", "Branch": "排版", "From": ["制定计划"] },
                { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["制定计划", "撰写", "排版"], "OnReject": "Retry", "MaxAttempts": 2 }
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
                { "Name": "规划者" },
                { "Name": "实施者" },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan", "Gate": "Review" },
                { "Name": "撰写", "Model": "实施者", "Mode": "PerItem", "Branch": "撰写", "From": ["制定计划"] },
                { "Name": "排版", "Model": "实施者", "Mode": "PerItem", "Branch": "排版", "From": ["制定计划"] },
                { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["制定计划", "撰写", "排版"], "OnReject": "Retry", "MaxAttempts": 2 }
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
                { "Name": "规划者" },
                { "Name": "实施者" },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan", "Gate": "Review" },
                { "Name": "撰写", "Model": "实施者", "Mode": "PerItem", "Branch": "撰写", "From": ["制定计划"] },
                { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["制定计划", "撰写"], "OnReject": "Retry", "MaxAttempts": 2 }
              ]
            }
          ]
        }
        """;

    private const string ParallelPerItemCheckFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者" },
                { "Name": "实施者" },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
                { "Name": "撰写", "Model": "实施者", "Mode": "PerItem", "Branch": "撰写", "From": ["制定计划"] },
                { "Name": "排版", "Model": "实施者", "Mode": "PerItem", "Branch": "排版", "From": ["制定计划"] },
                { "Name": "逐条检查", "Model": "检查者", "Output": "Review", "Mode": "PerItem", "From": ["撰写", "排版"], "OnReject": "Retry", "MaxAttempts": 2 }
              ]
            }
          ]
        }
        """;

    private const string StaticSplitFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "执行者" },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Model": "执行者", "Output": "Plan",
                  "Split": { "Items": [ { "Title": "甲", "Instruction": "做甲", "Acceptance": "甲可见" }, { "Title": "乙", "Instruction": "做乙", "Acceptance": "乙可见" } ], "ExtrasMax": 0 } },
                { "Name": "分配执行", "Model": "执行者", "Tools": ["GetLocalTime", "GetWeather"], "Mode": "PerItem", "From": ["制定计划"] },
                { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["制定计划", "分配执行"], "OnReject": "Retry" }
              ]
            }
          ]
        }
        """;

    private const string ExtrasSplitFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "执行者" },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Model": "执行者", "Output": "Plan",
                  "Split": { "Items": [ { "Title": "固定任务", "Instruction": "做固定", "Acceptance": "固定验收" } ], "ExtrasMax": 2, "Acceptance": "统一验收" } },
                { "Name": "分配执行", "Model": "执行者", "Tools": ["GetLocalTime", "GetWeather"], "Mode": "PerItem", "From": ["制定计划"] },
                { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["制定计划", "分配执行"], "OnReject": "Retry" }
              ]
            }
          ]
        }
        """;

    private const string ConstrainedSplitFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "执行者" },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Model": "执行者", "Output": "Plan", "Split": { "ExtrasMax": 1 } },
                { "Name": "分配执行", "Model": "执行者", "Tools": ["GetLocalTime", "GetWeather"], "Mode": "PerItem", "From": ["制定计划"] },
                { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["制定计划", "分配执行"], "OnReject": "Retry" }
              ]
            }
          ]
        }
        """;

    private const string StaticParallelSplitFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "执行者" },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Model": "执行者", "Output": "Plan",
                  "Split": { "ExtrasMax": 0, "Items": [
                    { "Title": "甲", "Instruction": "做甲", "Branch": "撰写" },
                    { "Title": "乙", "Instruction": "做乙", "Branch": "排版" } ] } },
                { "Name": "撰写", "Model": "执行者", "Mode": "PerItem", "Branch": "撰写", "From": ["制定计划"] },
                { "Name": "排版", "Model": "执行者", "Mode": "PerItem", "Branch": "排版", "From": ["制定计划"] },
                { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["制定计划", "撰写", "排版"], "OnReject": "Retry" }
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
                { "Name": "规划者" },
                { "Name": "实施者" },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定A计划", "Model": "规划者", "Output": "Plan" },
                { "Name": "制定B计划", "Model": "规划者", "Output": "Plan" },
                { "Name": "实施A", "Model": "实施者", "Mode": "PerItem", "From": ["制定A计划"] },
                { "Name": "实施B", "Model": "实施者", "Mode": "PerItem", "From": ["制定B计划"] },
                { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["制定A计划", "实施A", "制定B计划", "实施B"], "OnReject": "Retry" }
              ]
            }
          ]
        }
        """;

    private const string MultiPlanItemsJson = """
        [
          { "Title": "甲", "Instruction": "做甲", "Acceptance": "甲可见" },
          { "Title": "乙", "Instruction": "做乙", "Acceptance": "乙可见" }
        ]
        """;

    private const string SingleImplementReviewFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "实施者" },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "撰写", "Model": "实施者" },
                { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["撰写"], "OnReject": "Retry" }
              ]
            }
          ]
        }
        """;
}
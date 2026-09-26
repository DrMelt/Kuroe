using Kuroe.Shared.Agent;
using Kuroe.Shared.Agent.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Tests;

/// <summary>叶子链推进：展开、收拢、放行、返工、未收口与取消。默认流程即展开后整体检查。</summary>
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
        // 整体检查由单个 agent 汇拢全部条目实施
        Assert.Single(done.Nodes[2].Runs);
        Assert.Equal(3, done.FrontierNodes);
        Assert.All(done.Units.Where(unit => unit.ItemIndex is not null),
            unit => Assert.Equal(UnitVerdict.Verified, unit.Verdict));
    }

    [Fact]
    public void Rejected_funnel_check_retries_all_items_with_findings()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.CheckPasses = run => run.Context.Attempt > 1;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);

        // 两条目各实施两轮，整体检查两轮
        Assert.Equal(4, done.Nodes[1].Runs.Count);
        Assert.Equal(2, done.Nodes[2].Runs.Count);
        Assert.All(done.Units.Where(unit => unit.ItemIndex is not null), unit =>
        {
            Assert.Equal(2, unit.Attempts);
            Assert.Equal(UnitVerdict.Verified, unit.Verdict);
        });

        RunSnapshot retry = Assert.Single(done.Nodes[1].Runs,
            run => run.Context.ItemIndex == 0 && run.Context.Attempt == 2);
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
        // 逐条检查：每个条目各一个检查 agent
        Assert.Equal(2, done.Nodes[2].Runs.Count);
        Assert.All(done.Units.Where(unit => unit.ItemIndex is not null),
            unit => Assert.Equal(UnitVerdict.Verified, unit.Verdict));
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
    public void Stop_reject_action_parks_units_until_rework()
    {
        using KuroeHarness harness = KuroeHarness.Create(StopOnRejectFlow);
        harness.Executor.CheckPasses = _ => false;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);
        Assert.Equal(TaskState.Blocked, blocked.State);

        harness.Tasks.Rework(id, null).ThrowIfError();
        // 返工退回实施叶，重跑一轮实施后再次整体检查并停留
        TaskSnapshot reopened = harness.Wait(id, snapshot =>
            snapshot.Units.Any(unit => unit.ItemIndex == 0 && unit.Attempts > 1));

        Assert.Equal(2, Assert.Single(reopened.Units, unit => unit.ItemIndex == 0).Attempts);
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
        Assert.DoesNotContain(canceled.Units, unit => unit.State is UnitState.Blocked or UnitState.AwaitingApproval);
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
    private const string ReviewOnPlanFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Agents": [
                { "Name": "规划者" },
                { "Name": "执行者", "Tools": ["GetLocalTime", "GetWeather"] },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Agent": "规划者", "Output": "Plan", "Gate": "Review" },
                { "Name": "分配执行", "Agent": "执行者", "Mode": "PerItem", "From": ["制定计划"] },
                { "Name": "整体检查", "Agent": "检查者", "Output": "Review", "From": ["制定计划", "分配执行"], "OnReject": "Retry", "MaxAttempts": 2 }
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
              "Agents": [
                { "Name": "规划者" },
                { "Name": "执行者", "Tools": ["GetLocalTime", "GetWeather"] },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Agent": "规划者", "Output": "Plan" },
                { "Name": "分配执行", "Agent": "执行者", "Mode": "PerItem", "From": ["制定计划"] },
                { "Name": "整体检查", "Agent": "检查者", "Output": "Review", "From": ["制定计划", "分配执行"], "OnReject": "Stop" }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void Parallel_dispatch_assigns_items_to_branches_and_funnels()
    {
        using KuroeHarness harness = KuroeHarness.Create(ParallelFunnelFlow);
        harness.Executor.ItemsJson = BranchedItemsJson;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 规划、分配各一个 agent，两个分支各一个实施者，整体检查一个
        Assert.Single(done.Nodes[0].Runs);
        Assert.Single(done.Nodes[1].Runs);
        Assert.Single(done.Nodes[2].Runs);
        Assert.Single(done.Nodes[3].Runs);
        Assert.Single(done.Nodes[4].Runs);
        Assert.All(done.Units.Where(unit => unit.Branch is not null), unit =>
        {
            Assert.Equal(UnitVerdict.Verified, unit.Verdict);
            Assert.True(unit.Branch == "撰写" || unit.Branch == "排版", $"分支 {unit.Branch} 不在实施分支里。");
        });
    }

    [Fact]
    public void Parallel_funnel_reject_returns_each_item_to_its_branch()
    {
        using KuroeHarness harness = KuroeHarness.Create(ParallelFunnelFlow);
        harness.Executor.ItemsJson = BranchedItemsJson;
        harness.Executor.CheckPasses = run => run.Context.Attempt > 1;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 两个分支的实施各两轮，整体检查两轮
        Assert.Equal(2, done.Nodes[2].Runs.Count);
        Assert.Equal(2, done.Nodes[3].Runs.Count);
        Assert.Equal(2, done.Nodes[4].Runs.Count);
        Assert.All(done.Units.Where(unit => unit.Branch is not null), unit => Assert.Equal(2, unit.Attempts));

        // 退回后的实施上下文带上整体检查意见
        RunSnapshot retry = Assert.Single(done.Nodes[2].Runs, run => run.Context.Attempt == 2);
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
        // 段后逐条检查：每个条目各一个检查 agent
        Assert.Equal(2, done.Nodes[4].Runs.Count);
        Assert.All(done.Units.Where(unit => unit.Branch is not null),
            unit => Assert.Equal(UnitVerdict.Verified, unit.Verdict));
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
        // 批准推进必须展开并行段而不是整叶顶替分支：每个分支各一个实施者、整体检查一个
        Assert.Single(done.Nodes[2].Runs);
        Assert.Single(done.Nodes[3].Runs);
        Assert.Single(done.Nodes[4].Runs);
        Assert.All(done.Units.Where(unit => unit.Branch is not null),
            unit => Assert.Equal(UnitVerdict.Verified, unit.Verdict));
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
        // 单分支并行段也要按拆分展开：两条目各一个实施者
        Assert.Equal(2, done.Nodes[2].Runs.Count);
        Assert.Single(done.Nodes[3].Runs);
    }

    [Fact]
    public void Plan_dispatch_instruction_lists_available_parallel_branches()
    {
        using KuroeHarness harness = KuroeHarness.Create(ParallelFunnelFlow);
        harness.Executor.ItemsJson = BranchedItemsJson;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 分配者的上下文里给出可用的分支名，模型据此交回带 Branch 的条目
        RunSnapshot dispatcher = done.Nodes[1].Runs[0];
        Assert.Contains("可选分支：撰写、排版", dispatcher.Context.Instruction);
    }

    [Fact]
    public void Static_split_expands_without_plan_agent()
    {
        using KuroeHarness harness = KuroeHarness.Create(StaticSplitFlow);

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 静态拆分不派规划 agent，两条目各一个实施者，整体检查一个
        Assert.Empty(done.Nodes[0].Runs);
        Assert.Equal(2, done.Nodes[1].Runs.Count);
        Assert.Single(done.Nodes[2].Runs);
        Assert.All(done.Units.Where(unit => unit.ItemIndex is not null),
            unit => Assert.Equal(UnitVerdict.Verified, unit.Verdict));

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
        // 规划 agent 跑一次交补充条目，固定条目加两条补充共三个实施者
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
        // 静态拆分不派规划 agent，条目按声明的分支落并行分支各一个实施者
        Assert.Empty(done.Nodes[0].Runs);
        Assert.Single(done.Nodes[1].Runs);
        Assert.Single(done.Nodes[2].Runs);
        Assert.Single(done.Nodes[3].Runs);
        Assert.Equal(2, done.Units.Count(unit => unit.Branch is not null));
        Assert.All(done.Units.Where(unit => unit.Branch is not null),
            unit => Assert.Equal(UnitVerdict.Verified, unit.Verdict));
    }

    private const string BranchedItemsJson = """
        [
          { "Title": "甲", "Instruction": "做甲", "Acceptance": "甲可见", "Branch": "撰写" },
          { "Title": "乙", "Instruction": "做乙", "Acceptance": "乙可见", "Branch": "排版" }
        ]
        """;

    private const string StaticSplitFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Agents": [
                { "Name": "执行者" },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "静态拆分", "Agent": "执行者", "Output": "Plan",
                  "Split": {
                    "Items": [
                      { "Title": "甲", "Instruction": "做甲", "Acceptance": "甲可见" },
                      { "Title": "乙", "Instruction": "做乙", "Acceptance": "乙可见" }
                    ]
                  } },
                { "Name": "分配执行", "Agent": "执行者", "Mode": "PerItem", "From": ["静态拆分"] },
                { "Name": "整体检查", "Agent": "检查者", "Output": "Review", "From": ["静态拆分", "分配执行"], "OnReject": "Retry", "MaxAttempts": 2 }
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
              "Agents": [
                { "Name": "规划者" },
                { "Name": "执行者" },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Agent": "规划者", "Output": "Plan",
                  "Split": {
                    "Items": [ { "Title": "固定任务", "Instruction": "固定做法", "Acceptance": "固定验收" } ],
                    "ExtrasMax": 2,
                    "Acceptance": "统一验收"
                  } },
                { "Name": "分配执行", "Agent": "执行者", "Mode": "PerItem", "From": ["制定计划"] },
                { "Name": "整体检查", "Agent": "检查者", "Output": "Review", "From": ["制定计划", "分配执行"], "OnReject": "Retry", "MaxAttempts": 2 }
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
              "Agents": [
                { "Name": "规划者" },
                { "Name": "执行者" },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Agent": "规划者", "Output": "Plan",
                  "Split": { "ExtrasMax": 1, "Acceptance": "统一验收" } },
                { "Name": "分配执行", "Agent": "执行者", "Mode": "PerItem", "From": ["制定计划"] },
                { "Name": "整体检查", "Agent": "检查者", "Output": "Review", "From": ["制定计划", "分配执行"], "OnReject": "Retry" }
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
              "Agents": [
                { "Name": "执行者" },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "静态拆分", "Agent": "执行者", "Output": "Plan",
                  "Split": {
                    "Items": [
                      { "Title": "甲", "Instruction": "做甲", "Acceptance": "甲可见", "Branch": "撰写" },
                      { "Title": "乙", "Instruction": "做乙", "Acceptance": "乙可见", "Branch": "排版" }
                    ]
                  } },
                { "Name": "实施", "Mode": "Parallel",
                  "Nodes": [
                    { "Name": "撰写", "Agent": "执行者", "Mode": "PerItem", "From": ["静态拆分"] },
                    { "Name": "排版", "Agent": "执行者", "Mode": "PerItem", "From": ["静态拆分"] }
                  ] },
                { "Name": "整体检查", "Agent": "检查者", "Output": "Review", "From": ["静态拆分", "撰写", "排版"], "OnReject": "Retry" }
              ]
            }
          ]
        }
        """;

    private const string SingleBranchItemsJson = """
        [
          { "Title": "甲", "Instruction": "做甲", "Acceptance": "甲可见", "Branch": "撰写" },
          { "Title": "乙", "Instruction": "做乙", "Acceptance": "乙可见", "Branch": "撰写" }
        ]
        """;

    private const string ParallelFunnelFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Agents": [
                { "Name": "规划者" },
                { "Name": "分配者" },
                { "Name": "实施者" },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Agent": "规划者", "Output": "Plan" },
                { "Name": "分配任务", "Agent": "分配者", "Output": "Plan", "From": ["制定计划"] },
                { "Name": "实施", "Mode": "Parallel",
                  "Nodes": [
                    { "Name": "撰写", "Agent": "实施者", "Mode": "PerItem", "From": ["分配任务"] },
                    { "Name": "排版", "Agent": "实施者", "Mode": "PerItem", "From": ["分配任务"] }
                  ] },
                { "Name": "整体检查", "Agent": "检查者", "Output": "Review", "From": ["分配任务", "撰写", "排版"], "OnReject": "Retry" }
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
              "Agents": [
                { "Name": "规划者" },
                { "Name": "分配者" },
                { "Name": "实施者" },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Agent": "规划者", "Output": "Plan" },
                { "Name": "分配任务", "Agent": "分配者", "Output": "Plan", "From": ["制定计划"], "Gate": "Review" },
                { "Name": "实施", "Mode": "Parallel",
                  "Nodes": [
                    { "Name": "撰写", "Agent": "实施者", "Mode": "PerItem", "From": ["分配任务"] },
                    { "Name": "排版", "Agent": "实施者", "Mode": "PerItem", "From": ["分配任务"] }
                  ] },
                { "Name": "整体检查", "Agent": "检查者", "Output": "Review", "From": ["分配任务", "撰写", "排版"], "OnReject": "Retry" }
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
              "Agents": [
                { "Name": "规划者" },
                { "Name": "分配者" },
                { "Name": "实施者" },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Agent": "规划者", "Output": "Plan" },
                { "Name": "分配任务", "Agent": "分配者", "Output": "Plan", "From": ["制定计划"], "Gate": "Review" },
                { "Name": "实施", "Mode": "Parallel",
                  "Nodes": [
                    { "Name": "撰写", "Agent": "实施者", "Mode": "PerItem", "From": ["分配任务"] }
                  ] },
                { "Name": "整体检查", "Agent": "检查者", "Output": "Review", "From": ["分配任务", "撰写"], "OnReject": "Retry" }
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
              "Agents": [
                { "Name": "规划者" },
                { "Name": "分配者" },
                { "Name": "实施者" },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Agent": "规划者", "Output": "Plan" },
                { "Name": "分配任务", "Agent": "分配者", "Output": "Plan", "From": ["制定计划"] },
                { "Name": "实施", "Mode": "Parallel",
                  "Nodes": [
                    { "Name": "撰写", "Agent": "实施者", "Mode": "PerItem", "From": ["分配任务"] },
                    { "Name": "排版", "Agent": "实施者", "Mode": "PerItem", "From": ["分配任务"] }
                  ] },
                { "Name": "逐任务检查", "Agent": "检查者", "Output": "Review", "Mode": "PerItem",
                  "From": ["撰写", "排版"], "OnReject": "Retry", "MaxAttempts": 2 }
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
              "Agents": [
                { "Name": "规划者" },
                { "Name": "执行者", "Tools": ["GetLocalTime", "GetWeather"] },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Agent": "规划者", "Output": "Plan" },
                { "Name": "分配执行", "Agent": "执行者", "Mode": "PerItem", "From": ["制定计划"] },
                { "Name": "逐条检查", "Agent": "检查者", "Output": "Review", "Mode": "PerItem", "From": ["分配执行"], "OnReject": "Retry", "MaxAttempts": 2 }
              ]
            }
          ]
        }
        """;
}
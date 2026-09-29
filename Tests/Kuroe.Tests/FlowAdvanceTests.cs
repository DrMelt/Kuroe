using System.Threading;
using ErrorOr;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Tests;

/// <summary>图驱动推进：展开、汇拢、放行、返工、未收口与取消。默认流程即计划后按条目展开实施。</summary>
public sealed class FlowAdvanceTests
{
    [Fact]
    public void Builtin_flow_without_model_assignments_loads_but_submit_fails()
    {
        using KuroeHarness harness = KuroeHarness.Create(writeDefaultFlow: false);

        ErrorOr<TaskSnapshot> submitted = harness.Tasks.Submit("补齐 README", null, null);

        Assert.True(submitted.IsError);
        Assert.Contains(submitted.ErrorsOrEmptyList,
            error => error.Description.Contains("未写 Model"));
    }

    [Fact]
    public void Plan_implement_completes_task()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables[0].Runs);
        Assert.Equal(2, done.Executables[1].Runs.Count);
        Assert.Equal(2, done.FrontierNodes);
    }

    [Fact]
    public void Review_gate_parks_plan_until_approved()
    {
        using KuroeHarness harness = KuroeHarness.Create(ReviewOnPlanFlow);

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot parked = harness.Settle(id);
        Assert.Equal(TaskState.AwaitingApproval, parked.State);
        Assert.Single(parked.Executables[0].Runs);

        harness.Tasks.Approve(id).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(2, done.Executables[1].Runs.Count);
    }

    [Fact]
    public void Plan_step_without_submission_blocks_task()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.SubmitsPlan = false;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        RunSnapshot plan = Assert.Single(blocked.Executables[0].Runs);
        Assert.Equal(RunState.Failed, plan.State);
        Assert.Contains(plan.Failures, failure => failure.Contains("未收口"));
    }

    [Fact]
    public void Rework_with_unknown_item_keeps_task_blocked()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.FailsWhen = run => run.Context.NodeIndex == 3 && run.Context.ItemIndex == 0;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);
        Assert.Equal(TaskState.Blocked, blocked.State);

        // 指定的条目不属于阻塞目标时，返工视为未处理，任务保持阻塞
        Assert.True(harness.Tasks.Rework(id, 99).IsError);
        Assert.Equal(TaskState.Blocked, harness.Snapshot(id).State);
    }

    [Fact]
    public void Plan_uncollected_then_rework_reruns_plan()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.SubmitsPlan = false;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Single(blocked.Executables[0].Runs);

        harness.Executor.SubmitsPlan = true;
        harness.Tasks.Rework(id, null).ThrowIfError();

        TaskSnapshot done = harness.Wait(id, snapshot => snapshot.State == TaskState.Done);
        Assert.Equal(2, done.Executables[0].Runs.Count);
    }

    [Fact]
    public void Run_failure_then_rework_reruns_the_instance()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        int failures = 0;
        harness.Executor.FailsWhen = run =>
            run.Context.NodeIndex == 3 && Interlocked.Increment(ref failures) <= 2;

        TaskId id = harness.Submit("补齐 README");
        harness.Wait(id, snapshot => snapshot.State == TaskState.Blocked);

        harness.Tasks.Rework(id, null).ThrowIfError();
        TaskSnapshot done = harness.Wait(id, snapshot => snapshot.State == TaskState.Done);

        Assert.Equal(TaskState.Done, done.State);
        // 实施首次一轮失败，返工后第二轮重跑两个实例并正常收口
        Assert.Equal(4, done.Executables[1].Runs.Count);
    }

    [Fact]
    public void Cancel_after_block_settles_the_task()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.FailsWhen = run => run.Context.NodeIndex == 3;

        TaskId id = harness.Submit("补齐 README");
        harness.Wait(id, snapshot => snapshot.State == TaskState.Blocked);

        harness.Tasks.StopTask(id).ThrowIfError();
        TaskSnapshot canceled = harness.Settle(id);

        Assert.Equal(TaskState.Canceled, canceled.State);
        Assert.DoesNotContain(canceled.ExecutableStates,
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
    public void Parallel_dispatch_assigns_items_to_branches()
    {
        using KuroeHarness harness = KuroeHarness.Create(ParallelFunnelFlow);
        harness.Executor.ItemsJson = BranchedItemsJson;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

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
        using KuroeHarness harness = KuroeHarness.Create(ParallelFunnelFlow);

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Contains(harness.Executor.Submissions, text => text.Contains("必须写明分支 Branch"));
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
        // 批准推进必须展开分支而不是整节点顶替：每个分支各一个实施者
        Assert.Single(done.Executables[1].Runs);
        Assert.Single(done.Executables[2].Runs);
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
        Assert.Equal(2, done.Executables[1].Runs.Count);
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
        RunSnapshot planner = done.Executables[0].Runs[0];
        Assert.Contains("可选分支：撰写、排版", planner.Context.Instruction);
    }

    [Fact]
    public void Static_split_expands_without_plan_run()
    {
        using KuroeHarness harness = KuroeHarness.Create(StaticSplitFlow);

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 静态拆分不派规划 run，两条目各一个实施者
        Assert.Empty(done.Executables[0].Runs);
        Assert.Equal(2, done.Executables[1].Runs.Count);

        RunSnapshot first = done.Executables[1].Runs[0];
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
        Assert.Single(done.Executables[0].Runs);
        Assert.Equal(3, done.Executables[1].Runs.Count);

        // 规划指令给出固定条目参考与补充上限
        RunSnapshot plan = done.Executables[0].Runs[0];
        Assert.Contains("已按标准固定 1 条", plan.Context.Instruction);
        Assert.Contains("补充至多 2 条", plan.Context.Instruction);
        Assert.Contains("验收统一为", plan.Context.Instruction);

        // 固定条目保留配置内容，补充条目接受模型文本并注入统一验收
        RunSnapshot fixedRun = Assert.Single(done.Executables[1].Runs,
            run => run.Context.Seed.Any(message => message.Text.Contains("本条目：固定任务")));
        Assert.Contains(fixedRun.Context.Seed, message => message.Text.Contains("验收标准：固定验收"));

        RunSnapshot extraRun = Assert.Single(done.Executables[1].Runs,
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
        Assert.Empty(done.Executables[0].Runs);
        Assert.Single(done.Executables[1].Runs, run => run.Context.ItemIndex == 0);
        Assert.Single(done.Executables[2].Runs, run => run.Context.ItemIndex == 1);
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
        Assert.Single(done.Executables[0].Runs);
        Assert.Single(done.Executables[1].Runs);
        Assert.Equal(2, done.Executables[2].Runs.Count);
        Assert.Equal(2, done.Executables[3].Runs.Count);
        // 条目号只在自己的规划空间里有效，两个实施各自从 0 起
        Assert.All(done.Executables[2].Runs, run => Assert.InRange(run.Context.ItemIndex!.Value, 0, 1));
        Assert.All(done.Executables[3].Runs, run => Assert.InRange(run.Context.ItemIndex!.Value, 0, 1));
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
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan", "Gate": "Review" },
                { "Name": "分配执行", "Model": "执行者", "Tools": ["GetLocalTime", "GetWeather"], "Mode": "PerItem", "From": ["制定计划"] }
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
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
                { "Name": "撰写", "Model": "实施者", "Mode": "PerItem", "Branch": "撰写", "From": ["制定计划"] },
                { "Name": "排版", "Model": "实施者", "Mode": "PerItem", "Branch": "排版", "From": ["制定计划"] }
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
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan", "Gate": "Review" },
                { "Name": "撰写", "Model": "实施者", "Mode": "PerItem", "Branch": "撰写", "From": ["制定计划"] },
                { "Name": "排版", "Model": "实施者", "Mode": "PerItem", "Branch": "排版", "From": ["制定计划"] }
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
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan", "Gate": "Review" },
                { "Name": "撰写", "Model": "实施者", "Mode": "PerItem", "Branch": "撰写", "From": ["制定计划"] }
                  ]
                }
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
                { "Name": "执行者", "Model": "fake" }
              ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                { "Name": "制定计划", "Model": "执行者", "Output": "Plan",
                  "Split": { "Items": [ { "Title": "甲", "Instruction": "做甲", "Acceptance": "甲可见" }, { "Title": "乙", "Instruction": "做乙", "Acceptance": "乙可见" } ], "ExtrasMax": 0 } },
                { "Name": "分配执行", "Model": "执行者", "Tools": ["GetLocalTime", "GetWeather"], "Mode": "PerItem", "From": ["制定计划"] }
                  ]
                }
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
                { "Name": "执行者", "Model": "fake" }
              ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                { "Name": "制定计划", "Model": "执行者", "Output": "Plan",
                  "Split": { "Items": [ { "Title": "固定任务", "Instruction": "做固定", "Acceptance": "固定验收" } ], "ExtrasMax": 2, "Acceptance": "统一验收" } },
                { "Name": "分配执行", "Model": "执行者", "Tools": ["GetLocalTime", "GetWeather"], "Mode": "PerItem", "From": ["制定计划"] }
                  ]
                }
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
                { "Name": "执行者", "Model": "fake" }
              ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                { "Name": "制定计划", "Model": "执行者", "Output": "Plan", "Split": { "ExtrasMax": 1 } },
                { "Name": "分配执行", "Model": "执行者", "Tools": ["GetLocalTime", "GetWeather"], "Mode": "PerItem", "From": ["制定计划"] }
                  ]
                }
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
                { "Name": "执行者", "Model": "fake" }
              ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                { "Name": "制定计划", "Model": "执行者", "Output": "Plan",
                  "Split": { "ExtrasMax": 0, "Items": [
                    { "Title": "甲", "Instruction": "做甲", "Branch": "撰写" },
                    { "Title": "乙", "Instruction": "做乙", "Branch": "排版" } ] } },
                { "Name": "撰写", "Model": "执行者", "Mode": "PerItem", "Branch": "撰写", "From": ["制定计划"] },
                { "Name": "排版", "Model": "执行者", "Mode": "PerItem", "Branch": "排版", "From": ["制定计划"] }
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
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                { "Name": "制定A计划", "Model": "规划者", "Output": "Plan" },
                { "Name": "制定B计划", "Model": "规划者", "Output": "Plan" },
                { "Name": "实施A", "Model": "实施者", "Mode": "PerItem", "From": ["制定A计划"] },
                { "Name": "实施B", "Model": "实施者", "Mode": "PerItem", "From": ["制定B计划"] }
                  ]
                }
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
}
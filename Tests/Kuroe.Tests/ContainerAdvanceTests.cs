using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Tests;

/// <summary>容器作为节点实体的推进：容器门控等待放行、容器汇合后才放下游。</summary>
public sealed class ContainerAdvanceTests
{
    [Fact]
    public void Container_gate_parks_until_approved()
    {
        using KuroeHarness harness = KuroeHarness.Create(ContainerGateFlow);
        harness.Executor.ItemsJson = BranchedItemsJson;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot parked = harness.Settle(id);
        Assert.Equal(TaskState.AwaitingApproval, parked.State);
        // 成员全部完成，收拢检查等容器放行，还没有 run
        Assert.Single(parked.Executables[1].Runs);
        Assert.Single(parked.Executables[2].Runs);
        Assert.Empty(parked.Executables[3].Runs);
        ContainerSnapshot container = Assert.Single(parked.Containers);
        Assert.Equal(NodeState.AwaitingApproval, container.State);

        harness.Tasks.Approve(id).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables[3].Runs);
        Assert.All(done.ItemStates, state => Assert.Equal(UnitVerdict.Verified, state.Verdict));
    }

    [Fact]
    public void Container_join_releases_downstream_after_all_members()
    {
        using KuroeHarness harness = KuroeHarness.Create(ContainerJoinFlow);
        harness.Executor.ItemsJson = BranchedItemsJson;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 两条分支各一个 run，收拢检查等容器齐备后一条 run 汇拢
        Assert.Single(done.Executables[1].Runs);
        Assert.Single(done.Executables[2].Runs);
        Assert.Single(done.Executables[3].Runs);
        // 容器的快照状态汇拢后完成
        ContainerSnapshot container = Assert.Single(done.Containers);
        Assert.Equal(NodeState.Done, container.State);
    }

    [Fact]
    public void Container_source_context_carries_every_member_output()
    {
        using KuroeHarness harness = KuroeHarness.Create(ContainerJoinFlow);
        harness.Executor.ItemsJson = BranchedItemsJson;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);
        RunSnapshot check = Assert.Single(done.Executables[3].Runs);

        // 容器来源把成员产出一并带进：PerItem 成员按实例
        Assert.Contains(check.Context.Seed, message => message.Text.Contains("条目「甲」在节点「撰写」的产出"));
        Assert.Contains(check.Context.Seed, message => message.Text.Contains("条目「乙」在节点「排版」的产出"));
    }

    [Fact]
    public void Review_container_inside_auto_container_parks_until_approved()
    {
        using KuroeHarness harness = KuroeHarness.Create(NestedReviewContainerFlow);

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot parked = harness.Settle(id);
        Assert.Equal(TaskState.AwaitingApproval, parked.State);
        Assert.Empty(parked.Executables[2].Runs);
        // 子容器停在待批，父容器不广播下游
        Assert.Equal(NodeState.AwaitingApproval, Assert.Single(parked.Containers, container => container.Path == "交付/撰写组").State);

        harness.Tasks.Approve(id).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 子容器获批后一容器广播接续到检查，只跑一轮
        Assert.Single(done.Executables[2].Runs);
        Assert.All(done.Containers, container => Assert.Equal(NodeState.Done, container.State));
    }

    [Fact]
    public void Review_executable_inside_container_parks_downstream_until_approved()
    {
        using KuroeHarness harness = KuroeHarness.Create(ReviewMemberFlow);

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot parked = harness.Settle(id);
        Assert.Equal(TaskState.AwaitingApproval, parked.State);
        Assert.Empty(parked.Executables[2].Runs);
        Assert.Equal(NodeState.Running, Assert.Single(parked.Containers).State);

        harness.Tasks.Approve(id).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables[2].Runs);
        Assert.Equal(NodeState.Done, Assert.Single(done.Containers).State);
    }

    [Fact]
    public void Nested_container_rebroadcasts_after_failed_check_rework()
    {
        using KuroeHarness harness = KuroeHarness.Create(NestedContainerReworkFlow);
        harness.Executor.CheckPasses = run => run.Context.ExecutionCount > 1;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 容器广播在成员作废复位后重新发出，检查第二轮重新激活
        Assert.Equal(2, done.Executables[0].Runs.Count);
        Assert.Equal(2, done.Executables[1].Runs.Count);
        Assert.Equal(2, done.Executables[2].Runs.Count);
    }

    [Fact]
    public void Snapshot_ordinal_counts_executables_not_graph_slots()
    {
        using KuroeHarness harness = KuroeHarness.Create(ContainerJoinFlow);
        harness.Executor.ItemsJson = BranchedItemsJson;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        // 容器参与编号后图序号有空洞，展示顺位按执行节点表取名次
        Assert.Equal(4, done.TotalNodes);
        Assert.Equal(1, done.OrdinalOf(done.Executables[0].Index));
        Assert.Equal(2, done.OrdinalOf(done.Executables[1].Index));
        Assert.Equal(3, done.OrdinalOf(done.Executables[2].Index));
        Assert.Equal(4, done.OrdinalOf(done.Executables[3].Index));
        Assert.Equal(5, done.OrdinalOf(1));
    }

    private const string BranchedItemsJson = """
        [
          { "Title": "甲", "Instruction": "做甲", "Acceptance": "甲可见", "Branch": "撰写" },
          { "Title": "乙", "Instruction": "做乙", "Acceptance": "乙可见", "Branch": "排版" }
        ]
        """;

    private const string ContainerGateFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "实施者", "Model": "fake" },
                { "Name": "检查者", "Model": "fake" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
                { "Name": "交付", "Gate": "Review", "Nodes": [
                  { "Name": "撰写", "Model": "实施者", "Mode": "PerItem", "Branch": "撰写", "From": ["制定计划"] },
                  { "Name": "排版", "Model": "实施者", "Mode": "PerItem", "Branch": "排版", "From": ["制定计划"] }
                ] },
                { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["制定计划", "交付"], "OnReject": "Retry" }
              ]
            }
          ]
        }
        """;

    private const string ContainerJoinFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "实施者", "Model": "fake" },
                { "Name": "检查者", "Model": "fake" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
                { "Name": "交付", "Nodes": [
                  { "Name": "撰写", "Model": "实施者", "Mode": "PerItem", "Branch": "撰写", "From": ["制定计划"] },
                  { "Name": "排版", "Model": "实施者", "Mode": "PerItem", "Branch": "排版", "From": ["制定计划"] }
                ] },
                { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["交付"], "OnReject": "Retry" }
              ]
            }
          ]
        }
        """;

    private const string NestedReviewContainerFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "实施者", "Model": "fake" },
                { "Name": "检查者", "Model": "fake" }
              ],
              "Nodes": [
                { "Name": "交付", "Nodes": [
                  { "Name": "撰写组", "Gate": "Review", "Nodes": [
                    { "Name": "撰写", "Model": "实施者" }
                  ] },
                  { "Name": "排版", "Model": "实施者" }
                ] },
                { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["交付"], "OnReject": "Retry" }
              ]
            }
          ]
        }
        """;

    private const string ReviewMemberFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "实施者", "Model": "fake" },
                { "Name": "检查者", "Model": "fake" }
              ],
              "Nodes": [
                { "Name": "交付", "Nodes": [
                  { "Name": "撰写", "Model": "实施者", "Gate": "Review" },
                  { "Name": "排版", "Model": "实施者" }
                ] },
                { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["交付"], "OnReject": "Retry" }
              ]
            }
          ]
        }
        """;

    private const string NestedContainerReworkFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "实施者", "Model": "fake" },
                { "Name": "检查者", "Model": "fake" }
              ],
              "Nodes": [
                { "Name": "交付", "Nodes": [
                  { "Name": "撰写组", "Nodes": [
                    { "Name": "撰写", "Model": "实施者" }
                  ] },
                  { "Name": "排版", "Model": "实施者" }
                ] },
                { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["交付"], "OnReject": "Retry" }
              ]
            }
          ]
        }
        """;
}
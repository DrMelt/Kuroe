using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Tests;

/// <summary>容器作为节点实体的推进：容器门控停在待批准、容器汇合后才放下游。</summary>
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
        // 成员全部完成，收拢节点等容器放行，还没有 run
        Assert.Single(parked.Executables[1].Runs);
        Assert.Single(parked.Executables[2].Runs);
        Assert.Empty(parked.Executables[3].Runs);
        ContainerSnapshot container = Assert.Single(parked.Containers, pending => pending.State == NodeState.AwaitingApproval);
        Assert.Equal(NodeState.AwaitingApproval, container.State);

        harness.Tasks.Approve(id).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables[3].Runs);
    }

    [Fact]
    public void Container_join_releases_downstream_after_all_members()
    {
        using KuroeHarness harness = KuroeHarness.Create(ContainerJoinFlow);
        harness.Executor.ItemsJson = BranchedItemsJson;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 两条分支各一个 run，收拢节点等容器齐备后一条 run 汇拢
        Assert.Single(done.Executables[1].Runs);
        Assert.Single(done.Executables[2].Runs);
        Assert.Single(done.Executables[3].Runs);
        // 根容器与交付容器的快照状态汇拢后完成
        Assert.Equal(2, done.Containers.Count);
        Assert.All(done.Containers, container => Assert.Equal(NodeState.Done, container.State));
    }

    [Fact]
    public void Container_source_context_carries_every_member_output()
    {
        using KuroeHarness harness = KuroeHarness.Create(ContainerJoinFlow);
        harness.Executor.ItemsJson = BranchedItemsJson;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);
        RunSnapshot check = Assert.Single(done.Executables[3].Runs);

        // 容器来源把成员命名段按 Out 端口转进：每条绑定端口一条容器端口消息
        Assert.Contains(check.Context.Seed, message => message.Text.Contains("节点「交付」的端口「撰写结论」产出"));
        Assert.Contains(check.Context.Seed, message => message.Text.Contains("节点「交付」的端口「排版结论」产出"));
    }

    [Fact]
    public void Review_container_inside_auto_container_parks_until_approved()
    {
        using KuroeHarness harness = KuroeHarness.Create(NestedReviewContainerFlow);

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot parked = harness.Settle(id);
        Assert.Equal(TaskState.AwaitingApproval, parked.State);
        Assert.Empty(parked.Executables[2].Runs);
        // 子容器停在待批准，父容器不广播下游
        Assert.Equal(NodeState.AwaitingApproval, Assert.Single(parked.Containers, container => container.Path.Value == "整体/交付/撰写组").State);

        harness.Tasks.Approve(id).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 子容器获得批准后，容器广播接续到收拢节点，只跑一轮
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
        Assert.Equal(2, parked.Containers.Count);
        Assert.All(parked.Containers, container => Assert.Equal(NodeState.Running, container.State));

        harness.Tasks.Approve(id).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables[2].Runs);
        Assert.Equal(2, done.Containers.Count);
        Assert.All(done.Containers, container => Assert.Equal(NodeState.Done, container.State));
    }

    [Fact]
    public void Nested_container_rebroadcasts_after_failed_rework()
    {
        using KuroeHarness harness = KuroeHarness.Create(NestedContainerReworkFlow);
        harness.Executor.FailsWhen = run => run.Context.NodeIndex == 3 && run.Context.ExecutionCount == 1;

        TaskId id = harness.Submit("补齐 README");
        harness.Wait(id, snapshot => snapshot.State == TaskState.Blocked);
        harness.Tasks.Rework(id, null).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 容器广播在成员作废复位后重新发出，重跑的撰写重新激活下游
        Assert.Equal(2, done.Executables[0].Runs.Count);
        Assert.Single(done.Executables[1].Runs);
    }

    [Fact]
    public void Snapshot_ordinal_counts_executables_not_graph_slots()
    {
        using KuroeHarness harness = KuroeHarness.Create(ContainerJoinFlow);
        harness.Executor.ItemsJson = BranchedItemsJson;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        // 容器参与编号后图序号有空洞，展示顺位按执行节点表取名次
        Assert.Equal(4, done.TotalExecutableNodes);
        Assert.Equal(1, done.OrdinalOf(done.Executables[0].Index));
        Assert.Equal(2, done.OrdinalOf(done.Executables[1].Index));
        Assert.Equal(3, done.OrdinalOf(done.Executables[2].Index));
        Assert.Equal(4, done.OrdinalOf(done.Executables[3].Index));
        Assert.Equal(5, done.OrdinalOf(0));
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
                { "Name": "实施者", "Model": "fake" }
              ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
                { "Name": "交付", "Gate": "Review", "Out": { "撰写结论": "撰写@结论", "排版结论": "排版@结论" }, "Nodes": [
                  { "Name": "撰写", "Model": "实施者", "Mode": "PerItem", "Outputs": ["结论"], "Branch": "撰写", "From": ["制定计划@拆分"] },
                  { "Name": "排版", "Model": "实施者", "Mode": "PerItem", "Outputs": ["结论"], "Branch": "排版", "From": ["制定计划@拆分"] }
                ] },
                { "Name": "汇总", "Model": "实施者", "From": ["交付@撰写结论", "交付@排版结论"] }
                  ]
                }
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
                { "Name": "实施者", "Model": "fake" }
              ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
                { "Name": "交付", "Out": { "撰写结论": "撰写@结论", "排版结论": "排版@结论" }, "Nodes": [
                  { "Name": "撰写", "Model": "实施者", "Mode": "PerItem", "Outputs": ["结论"], "Branch": "撰写", "From": ["制定计划@拆分"] },
                  { "Name": "排版", "Model": "实施者", "Mode": "PerItem", "Outputs": ["结论"], "Branch": "排版", "From": ["制定计划@拆分"] }
                ] },
                { "Name": "汇总", "Model": "实施者", "From": ["交付@撰写结论", "交付@排版结论"] }
                  ]
                }
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
                { "Name": "实施者", "Model": "fake" }
              ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                { "Name": "交付", "Out": { "撰写结论": "撰写@结论", "排版结论": "排版@结论" }, "Nodes": [
                  { "Name": "撰写组", "Gate": "Review", "Nodes": [
                    { "Name": "撰写", "Model": "实施者", "Outputs": ["结论"] }
                  ] },
                  { "Name": "排版", "Model": "实施者", "Outputs": ["结论"] }
                ] },
                { "Name": "汇总", "Model": "实施者", "From": ["交付@撰写结论", "交付@排版结论"] }
                  ]
                }
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
                { "Name": "实施者", "Model": "fake" }
              ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                { "Name": "交付", "Out": { "撰写结论": "撰写@结论", "排版结论": "排版@结论" }, "Nodes": [
                  { "Name": "撰写", "Model": "实施者", "Outputs": ["结论"], "Gate": "Review" },
                  { "Name": "排版", "Model": "实施者", "Outputs": ["结论"] }
                ] },
                { "Name": "汇总", "Model": "实施者", "From": ["交付@撰写结论", "交付@排版结论"] }
                  ]
                }
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
                { "Name": "实施者", "Model": "fake" }
              ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                { "Name": "交付", "Out": { "撰写结论": "撰写@结论", "排版结论": "排版@结论" }, "Nodes": [
                  { "Name": "撰写组", "Nodes": [
                    { "Name": "撰写", "Model": "实施者", "Outputs": ["结论"] }
                  ] },
                  { "Name": "排版", "Model": "实施者", "Outputs": ["结论"] }
                ] },
                { "Name": "汇总", "Model": "实施者", "From": ["交付@撰写结论", "交付@排版结论"] }
                  ]
                }
              ]
            }
          ]
        }
        """;
}

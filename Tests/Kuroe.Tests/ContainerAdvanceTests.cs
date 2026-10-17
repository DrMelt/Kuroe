using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Runs;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.Shared.Waiting;
using Xunit;

namespace Kuroe.Tests;

/// <summary>容器作为节点实体的推进：审批节点停在待批准、容器汇合后才放下游。</summary>
public sealed class ContainerAdvanceTests
{
    [Fact]
    public void Approval_node_parks_until_approved()
    {
        using TestEnvironment environment = TestEnvironment.Create(ApprovalGateFlow);
        environment.Executor.ItemsJson = BranchedItemsJson;

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot parked = environment.Settle(id);
        Assert.Equal(TaskState.Running, parked.State);
        Assert.Single(environment.Awaiting(id, AwaitKind.Approve));
        // 两条分支各一个 run，审批节点停在待批准，汇总还没有 run
        Assert.Single(parked.Executables[1].Runs);
        Assert.Single(parked.Executables[2].Runs);
        Assert.Empty(parked.Executables[3].Runs);
        Assert.Empty(parked.Executables[4].Runs);
        Assert.Equal(NodeState.Done, Assert.Single(parked.Containers, container => container.Path.Name == new NodeName("交付")).State);

        environment.Waits.Approve(id).ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables[4].Runs);
        Assert.All(done.Containers, container => Assert.Equal(NodeState.Done, container.State));
    }

    [Fact]
    public void Container_join_releases_downstream_after_all_members()
    {
        using TestEnvironment environment = TestEnvironment.Create(ContainerJoinFlow);
        environment.Executor.ItemsJson = BranchedItemsJson;

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot done = environment.Settle(id);

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
        using TestEnvironment environment = TestEnvironment.Create(ContainerJoinFlow);
        environment.Executor.ItemsJson = BranchedItemsJson;

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot done = environment.Settle(id);
        RunSnapshot check = Assert.Single(done.Executables[3].Runs);

        // 容器来源把成员命名段按输出端口转进：每条绑定端口一条容器端口消息
        Assert.Contains(check.Context.Seed, message => message.Text.Contains("节点「整体/交付」的端口「撰写结论」产出"));
        Assert.Contains(check.Context.Seed, message => message.Text.Contains("节点「整体/交付」的端口「排版结论」产出"));
    }

    [Fact]
    public void Approval_inside_container_parks_until_approved()
    {
        using TestEnvironment environment = TestEnvironment.Create(NestedApprovalFlow);

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot parked = environment.Settle(id);
        Assert.Equal(TaskState.Running, parked.State);
        Assert.Single(environment.Awaiting(id, AwaitKind.Approve));
        Assert.Empty(parked.Executables[3].Runs);
        Assert.Equal(NodeState.Done, Assert.Single(parked.Containers, container => container.Path.Name == new NodeName("交付")).State);
        Assert.Equal(NodeState.Done, Assert.Single(parked.Containers, container => container.Path.Name == new NodeName("撰写组")).State);

        environment.Waits.Approve(id).ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 审批节点获得批准后，容器广播接续到收拢节点，只跑一轮
        Assert.Single(done.Executables[3].Runs);
        Assert.All(done.Containers, container => Assert.Equal(NodeState.Done, container.State));
    }

    [Fact]
    public void Approval_of_container_parks_downstream_until_approved()
    {
        using TestEnvironment environment = TestEnvironment.Create(ApprovalOfContainerFlow);

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot parked = environment.Settle(id);
        Assert.Equal(TaskState.Running, parked.State);
        Assert.Single(environment.Awaiting(id, AwaitKind.Approve));
        Assert.Empty(parked.Executables[3].Runs);
        Assert.Equal(NodeState.Done, Assert.Single(parked.Containers, container => container.Path.Name == new NodeName("交付")).State);

        environment.Waits.Approve(id).ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables[3].Runs);
        Assert.All(done.Containers, container => Assert.Equal(NodeState.Done, container.State));
    }

    [Fact]
    public void Nested_container_rebroadcasts_after_failed_rework()
    {
        using TestEnvironment environment = TestEnvironment.Create(NestedContainerReworkFlow);
        environment.Executor.FailsWhen = run => run.Context.Node.Name.Value == "撰写" && run.Context.ExecutionCount == 1;

        TaskId id = environment.Submit("补齐 README");
        environment.Wait(id, snapshot => snapshot.State == TaskState.Blocked);
        environment.Tasks.Rework(id, null).ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 容器广播在成员作废复位后重新发出，重跑的撰写重新激活下游
        Assert.Equal(2, done.Executables[0].Runs.Count);
        Assert.Single(done.Executables[1].Runs);
    }

    [Fact]
    public void Snapshot_ordinal_counts_executables_not_graph_slots()
    {
        using TestEnvironment environment = TestEnvironment.Create(ContainerJoinFlow);
        environment.Executor.ItemsJson = BranchedItemsJson;

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot done = environment.Settle(id);

        // 展示顺位按执行节点表取名次
        Assert.Equal(4, done.TotalExecutableNodes);
        Assert.Equal(1, done.OrdinalOf(done.Executables[0].Executable.Path));
        Assert.Equal(2, done.OrdinalOf(done.Executables[1].Executable.Path));
        Assert.Equal(3, done.OrdinalOf(done.Executables[2].Executable.Path));
        Assert.Equal(4, done.OrdinalOf(done.Executables[3].Executable.Path));
        Assert.Equal(5, done.OrdinalOf(NodePath.Of("Start")));
    }

    private const string BranchedItemsJson = """
        [
          { "Title": "甲", "Instruction": "做甲", "Acceptance": "甲可见", "Branch": "撰写" },
          { "Title": "乙", "Instruction": "做乙", "Acceptance": "乙可见", "Branch": "排版" }
        ]
        """;

    private const string ApprovalGateFlow = """
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
                { "Name": "交付", "Kind": "Container", "Outputs": [{ "Name": "撰写结论", "Ref": "撰写@结论" }, { "Name": "排版结论", "Ref": "排版@结论" }], "Nodes": [
                  { "Name": "撰写", "Kind": "Llm", "Model": "实施者", "Mode": "PerItem", "Outputs": [{ "Name": "结论", "Type": "Text" }], "Branch": "撰写", "From": [ { "拆分": "制定计划@拆分" } ] },
                  { "Name": "排版", "Kind": "Llm", "Model": "实施者", "Mode": "PerItem", "Outputs": [{ "Name": "结论", "Type": "Text" }], "Branch": "排版", "From": [ { "拆分": "制定计划@拆分" } ] }
                ] },
                { "Name": "审阅", "Kind": "Approval", "Question": "确认两版结论", "Inputs": [{ "Name": "撰写结论" }, { "Name": "排版结论" }], "From": [ { "撰写结论": "交付@撰写结论", "排版结论": "交付@排版结论" } ] },
                { "Name": "汇总", "Kind": "Llm", "Model": "实施者", "From": [ { "撰写结论": "审阅@撰写结论", "排版结论": "审阅@排版结论" } ] }
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
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                { "Name": "制定计划", "Kind": "Llm", "Model": "规划者", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] },
                { "Name": "交付", "Kind": "Container", "Outputs": [{ "Name": "撰写结论", "Ref": "撰写@结论" }, { "Name": "排版结论", "Ref": "排版@结论" }], "Nodes": [
                  { "Name": "撰写", "Kind": "Llm", "Model": "实施者", "Mode": "PerItem", "Outputs": [{ "Name": "结论", "Type": "Text" }], "Branch": "撰写", "From": [ { "拆分": "制定计划@拆分" } ] },
                  { "Name": "排版", "Kind": "Llm", "Model": "实施者", "Mode": "PerItem", "Outputs": [{ "Name": "结论", "Type": "Text" }], "Branch": "排版", "From": [ { "拆分": "制定计划@拆分" } ] }
                ] },
                { "Name": "汇总", "Kind": "Llm", "Model": "实施者", "From": [ { "撰写结论": "交付@撰写结论", "排版结论": "交付@排版结论" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private const string NestedApprovalFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "实施者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                { "Name": "交付", "Kind": "Container", "Outputs": [{ "Name": "撰写结论", "Ref": "撰写@结论" }, { "Name": "排版结论", "Ref": "排版@结论" }], "Nodes": [
                  { "Name": "撰写组", "Kind": "Container", "Nodes": [
                    { "Name": "撰写", "Kind": "Llm", "Model": "实施者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] }
                  ] },
                  { "Name": "排版", "Kind": "Llm", "Model": "实施者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] }
                ] },
                { "Name": "审阅", "Kind": "Approval", "Inputs": [{ "Name": "撰写结论" }, { "Name": "排版结论" }], "From": [ { "撰写结论": "交付@撰写结论", "排版结论": "交付@排版结论" } ] },
                { "Name": "汇总", "Kind": "Llm", "Model": "实施者", "From": [ { "撰写结论": "审阅@撰写结论", "排版结论": "审阅@排版结论" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private const string ApprovalOfContainerFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "实施者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                { "Name": "交付", "Kind": "Container", "Outputs": [{ "Name": "撰写结论", "Ref": "撰写@结论" }, { "Name": "排版结论", "Ref": "排版@结论" }], "Nodes": [
                  { "Name": "撰写", "Kind": "Llm", "Model": "实施者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
                  { "Name": "排版", "Kind": "Llm", "Model": "实施者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] }
                ] },
                { "Name": "审阅", "Kind": "Approval", "Inputs": [{ "Name": "撰写结论" }, { "Name": "排版结论" }], "From": [ { "撰写结论": "交付@撰写结论", "排版结论": "交付@排版结论" } ] },
                { "Name": "汇总", "Kind": "Llm", "Model": "实施者", "From": [ { "撰写结论": "审阅@撰写结论", "排版结论": "审阅@排版结论" } ] }
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
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                { "Name": "交付", "Kind": "Container", "Outputs": [{ "Name": "撰写结论", "Ref": "撰写@结论" }, { "Name": "排版结论", "Ref": "排版@结论" }], "Nodes": [
                  { "Name": "撰写组", "Kind": "Container", "Nodes": [
                    { "Name": "撰写", "Kind": "Llm", "Model": "实施者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] }
                  ] },
                  { "Name": "排版", "Kind": "Llm", "Model": "实施者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] }
                ] },
                { "Name": "汇总", "Kind": "Llm", "Model": "实施者", "From": [ { "撰写结论": "交付@撰写结论", "排版结论": "交付@排版结论" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;
}

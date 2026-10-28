using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Pending;
using Xunit;

namespace Kuroe.Tests;

/// <summary>容器作为节点实体的推进：审批节点停在待批准、容器汇合后才放下游。</summary>
public sealed class ContainerAdvanceTests
{
    [Fact]
    public void Approval_node_parks_until_approved()
    {
        using TestEnvironment environment = TestEnvironment.Create(ApprovalGateFlow);

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot parked = environment.Settle(id);
        Assert.Equal(TaskState.Running, parked.State);
        Assert.Single(environment.Awaiting(id, PendingKind.Approve));
        // 两条分支各一个执行，审批节点停在待批准，汇总还没有执行
        Assert.Single(parked.Executables[1].Executions);
        Assert.Single(parked.Executables[2].Executions);
        Assert.Empty(parked.Executables[3].Executions);
        Assert.Empty(parked.Executables[4].Executions);
        Assert.Equal(NodeState.Done, Assert.Single(parked.Containers, container => container.Path.Label == new NodeLabel("交付")).State);

        environment.Pendings.Approve(id).ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables[4].Executions);
        Assert.All(done.Containers, container => Assert.Equal(NodeState.Done, container.State));
    }

    [Fact]
    public void Container_join_releases_downstream_after_all_members()
    {
        using TestEnvironment environment = TestEnvironment.Create(ContainerJoinFlow);

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 两条分支各一个执行，收拢节点等容器齐备后一条执行汇拢
        Assert.Single(done.Executables[1].Executions);
        Assert.Single(done.Executables[2].Executions);
        Assert.Single(done.Executables[3].Executions);
        // 根容器与交付容器的快照状态汇拢后完成
        Assert.Equal(2, done.Containers.Count);
        Assert.All(done.Containers, container => Assert.Equal(NodeState.Done, container.State));
    }

    [Fact]
    public void Container_source_context_carries_every_member_output()
    {
        using TestEnvironment environment = TestEnvironment.Create(ContainerJoinFlow);

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot done = environment.Settle(id);
        ExecutionSnapshot check = Assert.Single(done.Executables[3].Executions);

        // 容器来源把成员命名段按输出端口转进：每条绑定端口一条容器端口取值
        Assert.Contains(check.Input.Inputs, value =>
            value.Origin is ContainerPortSource source && source.Port.Value == "撰写结论");
        Assert.Contains(check.Input.Inputs, value =>
            value.Origin is ContainerPortSource source && source.Port.Value == "排版结论");
    }

    [Fact]
    public void Approval_inside_container_parks_until_approved()
    {
        using TestEnvironment environment = TestEnvironment.Create(NestedApprovalFlow);

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot parked = environment.Settle(id);
        Assert.Equal(TaskState.Running, parked.State);
        Assert.Single(environment.Awaiting(id, PendingKind.Approve));
        Assert.Empty(parked.Executables[3].Executions);
        Assert.Equal(NodeState.Done, Assert.Single(parked.Containers, container => container.Path.Label == new NodeLabel("交付")).State);
        Assert.Equal(NodeState.Done, Assert.Single(parked.Containers, container => container.Path.Label == new NodeLabel("撰写组")).State);

        environment.Pendings.Approve(id).ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 审批节点获得批准后，容器广播接续到收拢节点，只跑一轮
        Assert.Single(done.Executables[3].Executions);
        Assert.All(done.Containers, container => Assert.Equal(NodeState.Done, container.State));
    }

    [Fact]
    public void Approval_of_container_parks_downstream_until_approved()
    {
        using TestEnvironment environment = TestEnvironment.Create(ApprovalOfContainerFlow);

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot parked = environment.Settle(id);
        Assert.Equal(TaskState.Running, parked.State);
        Assert.Single(environment.Awaiting(id, PendingKind.Approve));
        Assert.Empty(parked.Executables[3].Executions);
        Assert.Equal(NodeState.Done, Assert.Single(parked.Containers, container => container.Path.Label == new NodeLabel("交付")).State);

        environment.Pendings.Approve(id).ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables[3].Executions);
        Assert.All(done.Containers, container => Assert.Equal(NodeState.Done, container.State));
    }

    [Fact]
    public void Nested_container_rebroadcasts_after_failed_rework()
    {
        using TestEnvironment environment = TestEnvironment.Create(NestedContainerReworkFlow);
        environment.Runner.FailsWhen = execution => execution.Input.Node.Label.Value == "撰写" && execution.Input.ExecutionCount == 1;

        TaskId id = environment.Submit("补齐 README");
        environment.Wait(id, snapshot => snapshot.State == TaskState.Blocked);
        environment.Tasks.Rework(id).ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 容器广播在成员作废复位后重新发出，重跑的撰写重新激活下游
        Assert.Equal(2, done.Executables[0].Executions.Count);
        Assert.Single(done.Executables[1].Executions);
    }

    [Fact]
    public void Snapshot_ordinal_counts_executables_not_graph_slots()
    {
        using TestEnvironment environment = TestEnvironment.Create(ContainerJoinFlow);

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

    private const string ApprovalGateFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                { "Name": "制定计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                { "Name": "交付", "Kind": "Container", "Outputs": [{ "Name": "撰写结论", "Ref": "撰写@结论" }, { "Name": "排版结论", "Ref": "排版@结论" }], "Inputs": [{ "Name": "计划" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "计划": "制定计划@计划", "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
                  { "Name": "撰写", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "计划": "@计划" } ] },
                  { "Name": "排版", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "计划": "@计划" } ] }
                ] },
                { "Name": "审阅", "Kind": "Approval", "Question": "确认两版结论", "Inputs": [{ "Name": "撰写结论" }, { "Name": "排版结论" }], "From": [ { "撰写结论": "交付@撰写结论", "排版结论": "交付@排版结论" } ] },
                { "Name": "汇总", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "撰写结论": "审阅@撰写结论", "排版结论": "审阅@排版结论" } ] }
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
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                { "Name": "制定计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                { "Name": "交付", "Kind": "Container", "Outputs": [{ "Name": "撰写结论", "Ref": "撰写@结论" }, { "Name": "排版结论", "Ref": "排版@结论" }], "Inputs": [{ "Name": "计划" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "计划": "制定计划@计划", "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
                  { "Name": "撰写", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "计划": "@计划" } ] },
                  { "Name": "排版", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "计划": "@计划" } ] }
                ] },
                { "Name": "汇总", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "撰写结论": "交付@撰写结论", "排版结论": "交付@排版结论" } ] }
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
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                { "Name": "交付", "Kind": "Container", "Outputs": [{ "Name": "撰写结论", "Ref": "撰写@结论" }, { "Name": "排版结论", "Ref": "排版@结论" }], "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "@目标", "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
                  { "Name": "撰写组", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "@目标", "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
                    { "Name": "撰写", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] }
                  ] },
                  { "Name": "排版", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] }
                ] },
                { "Name": "审阅", "Kind": "Approval", "Inputs": [{ "Name": "撰写结论" }, { "Name": "排版结论" }], "From": [ { "撰写结论": "交付@撰写结论", "排版结论": "交付@排版结论" } ] },
                { "Name": "汇总", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "撰写结论": "审阅@撰写结论", "排版结论": "审阅@排版结论" } ] }
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
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                { "Name": "交付", "Kind": "Container", "Outputs": [{ "Name": "撰写结论", "Ref": "撰写@结论" }, { "Name": "排版结论", "Ref": "排版@结论" }], "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "@目标", "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
                  { "Name": "撰写", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                  { "Name": "排版", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] }
                ] },
                { "Name": "审阅", "Kind": "Approval", "Inputs": [{ "Name": "撰写结论" }, { "Name": "排版结论" }], "From": [ { "撰写结论": "交付@撰写结论", "排版结论": "交付@排版结论" } ] },
                { "Name": "汇总", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "撰写结论": "审阅@撰写结论", "排版结论": "审阅@排版结论" } ] }
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
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                { "Name": "交付", "Kind": "Container", "Outputs": [{ "Name": "撰写结论", "Ref": "撰写@结论" }, { "Name": "排版结论", "Ref": "排版@结论" }], "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "@目标", "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
                  { "Name": "撰写组", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "@目标", "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
                    { "Name": "撰写", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] }
                  ] },
                  { "Name": "排版", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] }
                ] },
                { "Name": "汇总", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "撰写结论": "交付@撰写结论", "排版结论": "交付@排版结论" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;
}

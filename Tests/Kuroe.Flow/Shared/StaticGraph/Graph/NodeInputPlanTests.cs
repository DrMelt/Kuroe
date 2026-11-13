using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.Shared.StaticGraph.Graph;
using Kuroe.Flow.Node.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Flow.Tasks.Shared;
using Xunit;

namespace Kuroe.Flow.Shared.Tests.StaticGraph.Graph;

/// <summary>输入侧方案与端口绑定：启动组、执行上限与绑定的形状。</summary>
public sealed class NodeInputPlanTests
{
    [Fact]
    public void Plan_defaults_to_the_shared_execution_limit()
    {
        NodeInputPlan plan = new();

        Assert.Equal(ExecutableNode.DefaultMaxExecutions, plan.MaxExecutions);
        Assert.Empty(plan.Groups);
    }

    [Fact]
    public void Group_is_driven_by_signals_only_when_a_binding_signals()
    {
        InputBinding value = new(NodePath.Of("乙"), BindingSource.ForNode(NodePath.Of("甲"), new PortName("结论")), new PortName("目标"), PortTypes.Text);
        InputBinding signal = new(NodePath.Of("乙"), BindingSource.ForNode(NodePath.Of("甲"), new PortName("信号")), new PortName("目标"), PortTypes.Signal);

        InputGroup plain = new([value]);
        InputGroup driven = new([value, signal]);

        Assert.False(plain.DrivenBySignals);
        Assert.Empty(plain.Signals);
        Assert.True(driven.DrivenBySignals);
        Assert.Equal([signal], driven.Signals);
    }

    [Fact]
    public void Binding_carries_source_port_target_input_and_group()
    {
        InputBinding binding = new(
            NodePath.Of("乙"),
            BindingSource.ForNode(NodePath.Of("甲"), new PortName("任务")),
            new PortName("目标"),
            PortTypes.Text,
            Group: 3);

        Assert.Equal(NodePath.Of("甲"), binding.Source.Node);
        Assert.Equal(NodePath.Of("乙"), binding.Target);
        Assert.Equal(new PortName("任务"), binding.SourcePort);
        Assert.Equal(new PortName("目标"), binding.TargetInput);
        Assert.Equal(PortTypes.Text, binding.Type);
        Assert.Equal(3, binding.Group);
    }

    [Fact]
    public void Plan_binding_carries_the_target_input()
    {
        InputBinding binding = new(
            NodePath.Of("乙"),
            BindingSource.ForNode(NodePath.Of("甲"), new PortName("结果")),
            new PortName("目标"),
            PortTypes.Text);
        NodeInputPlan plan = NodeInputPlan.Of(new ExecutableSpec(), [binding], [], []);

        InputBinding planned = Assert.Single(Assert.Single(plan.Groups).Bindings);
        Assert.Equal(new PortName("目标"), planned.TargetInput);
    }

    [Fact]
    public void Only_static_inputs_make_a_plan_start_without_dynamic_source()
    {
        NodeInputPlan plan = NodeInputPlan.Of(
            new ExecutableSpec(),
            [],
            [new InputPort(new PortName("配置"), PortTypes.Text, mode: PortMode.Constant)],
            []);

        Assert.True(plan.StaticOnly);
        Assert.Empty(plan.Groups);
    }

    [Fact]
    public void No_inputs_are_static_ready()
    {
        NodeInputPlan plan = NodeInputPlan.Of(new ExecutableSpec(), [], [], []);

        Assert.True(plan.StaticOnly);
        Assert.Empty(plan.Groups);
    }

    [Fact]
    public void Dynamic_inputs_do_not_start_without_a_source()
    {
        NodeInputPlan plan = NodeInputPlan.Of(
            new ExecutableSpec(),
            [],
            [new InputPort(new PortName("目标"), PortTypes.Text)],
            []);

        Assert.False(plan.StaticOnly);
    }

    [Fact]
    public void Container_plan_separates_content_sources_from_gates()
    {
        NodePath containerPath = NodePath.Of("整体");
        NodePath first = NodePath.Of("整体", "甲");
        NodePath second = NodePath.Of("整体", "乙");
        PortName gate = new("把关");
        ContainerNode container = new()
        {
            Path = containerPath,
            Type = new TestNodeType(),
            Children =
            [
                new ExecutableNode { Path = first, Type = new TestNodeType(), Spec = new ExecutableSpec() },
                new ExecutableNode { Path = second, Type = new TestNodeType(), Spec = new ExecutableSpec() },
            ],
            InputBindings = new Dictionary<PortName, InputBinding>
            {
                [gate] = new(containerPath, BindingSource.ForNode(NodePath.Of("来源"), new PortName("信号")), gate, PortTypes.Signal, Role: BindingRole.Gate),
            },
        };

        NodeInputPlan plan = NodeInputPlan.Of(container);

        Assert.Equal([first, second], plan.ContentBindings.Select(binding => binding.Source.Node));
        Assert.Equal(
            [BindingRole.Dynamic, BindingRole.Dynamic, BindingRole.Gate],
            Assert.Single(plan.Groups).Bindings.Select(binding => binding.Role));
    }

    [Fact]
    public void External_plan_has_no_driving_groups()
    {
        NodeInputPlan plan = NodeInputPlan.External();

        Assert.Equal(NodeStartMode.External, plan.StartMode);
        Assert.Empty(plan.Groups);
        Assert.Empty(plan.StaticBindings);
        Assert.Equal(int.MaxValue, plan.MaxExecutions);
    }

    private sealed class TestNodeType : INodeType
    {
        public NodeKind Kind => new("Container");

        public NodeTypeShape Shape => new() { Organizes = true };
    }
}

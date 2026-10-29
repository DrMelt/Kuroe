using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Flow.Shared.Tests;

/// <summary>输入侧方案与依赖边：启动组、执行上限与边的形状。</summary>
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
        InputBinding value = new(NodePath.Of("甲"), new PortName("结论"), PortTypes.Text);
        InputBinding signal = new(NodePath.Of("乙"), new PortName("信号"), PortTypes.Signal);

        InputGroup plain = new([value]);
        InputGroup driven = new([value, signal]);

        Assert.False(plain.DrivenBySignals);
        Assert.Empty(plain.Signals);
        Assert.True(driven.DrivenBySignals);
        Assert.Equal([signal], driven.Signals);
    }

    [Fact]
    public void Edge_carries_source_port_target_input_and_group()
    {
        FlowEdge edge = new(NodePath.Of("甲"), NodePath.Of("乙"), new PortName("任务"), new PortName("目标"), PortTypes.Text, 3);

        Assert.Equal(NodePath.Of("甲"), edge.Source);
        Assert.Equal(NodePath.Of("乙"), edge.Target);
        Assert.Equal(new PortName("任务"), edge.SourcePort);
        Assert.Equal(new PortName("目标"), edge.TargetInput);
        Assert.Equal(PortTypes.Text, edge.Type);
        Assert.Equal(3, edge.Group);
    }

    [Fact]
    public void Plan_binding_carries_the_target_input()
    {
        FlowEdge edge = new(NodePath.Of("甲"), NodePath.Of("乙"), new PortName("结果"), new PortName("目标"), PortTypes.Text);
        NodeInputPlan plan = NodeInputPlan.Of(new ExecutableSpec(), [edge], []);

        InputBinding binding = Assert.Single(Assert.Single(plan.Groups).Bindings);
        Assert.Equal(new PortName("目标"), binding.TargetInput);
    }

    [Fact]
    public void Only_static_inputs_make_a_plan_start_without_dynamic_source()
    {
        NodeInputPlan plan = NodeInputPlan.Of(
            new ExecutableSpec(),
            [],
            [new InputPort(new PortName("配置"), PortTypes.Text, isStatic: true)]);

        Assert.True(plan.StartsAtTaskStart);
        Assert.Empty(plan.Groups);
    }

    [Fact]
    public void Dynamic_inputs_do_not_start_without_a_source()
    {
        NodeInputPlan plan = NodeInputPlan.Of(
            new ExecutableSpec(),
            [],
            [new InputPort(new PortName("目标"), PortTypes.Text)]);

        Assert.False(plan.StartsAtTaskStart);
    }
}

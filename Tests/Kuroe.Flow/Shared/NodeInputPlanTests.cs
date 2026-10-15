using Kuroe.Flow.Shared.StaticGraph;
using Xunit;

namespace Kuroe.Flow.Shared.Tests;

/// <summary>输入侧方案与依赖边：实例空间、驱动与执行上限的缺省，启动组的派生成员。</summary>
public sealed class NodeInputPlanTests
{
    [Fact]
    public void Plan_defaults_to_a_single_versioned_instance()
    {
        NodeInputPlan plan = new();

        Assert.Equal(SpaceKind.None, plan.Space);
        Assert.Equal(NodeDriver.Version, plan.Driver);
        Assert.Equal(ExecutableNode.DefaultMaxRuns, plan.MaxRuns);
        Assert.Null(plan.SplitSource);
        Assert.Null(plan.Branch);
        Assert.Empty(plan.AlignedSources);
        Assert.Empty(plan.Groups);
    }

    [Fact]
    public void Group_is_driven_by_signals_only_when_a_binding_signals()
    {
        InputBinding value = new(EdgeKind.Value, 1, FeedScope.Whole, FeedRecurrence.Repeat, Signal: false);
        InputBinding signal = new(EdgeKind.Split, 2, FeedScope.Item, FeedRecurrence.Once, Signal: true);

        InputGroup plain = new([value]);
        InputGroup driven = new([value, signal]);

        Assert.False(plain.DrivenBySignals);
        Assert.Empty(plain.Signals);
        Assert.True(driven.DrivenBySignals);
        Assert.Equal([signal], driven.Signals);
    }

    [Fact]
    public void Split_edge_carries_the_space_role()
    {
        FlowEdge edge = FlowEdge.Split(1, 2, new PortName("任务"), new PortName("目标"), group: 3);

        Assert.Equal(1, edge.Source);
        Assert.Equal(2, edge.Target);
        Assert.Equal(EdgeKind.Split, edge.Kind);
        Assert.Equal(FeedScope.Whole, edge.Scope);
        Assert.Equal(FeedRecurrence.Repeat, edge.Recurrence);
        Assert.Equal(new PortName("任务"), edge.SourcePort);
        Assert.Equal(new PortName("目标"), edge.TargetInput);
        Assert.Equal(PortType.Text, edge.Type);
        Assert.Equal(3, edge.Group);
    }
}

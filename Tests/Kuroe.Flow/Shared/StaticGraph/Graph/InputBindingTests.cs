using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.Shared.StaticGraph.Graph;
using Kuroe.Flow.Node.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Tasks.Shared;
using Xunit;

namespace Kuroe.Flow.Shared.Tests.StaticGraph.Graph;

/// <summary>端口绑定的角色：动态驱动、门控、静态与别名在驱动、触发与齐备上的参与方式。</summary>
public sealed class InputBindingTests
{
    [Fact]
    public void Member_binding_carries_the_node_as_source()
    {
        InputBinding binding = InputBinding.Member(NodePath.Of("整体"), NodePath.Of("成员"));

        Assert.Equal(NodePath.Of("整体"), binding.Target);
        Assert.Equal(NodePath.Of("成员"), binding.Source.Node);
        Assert.Null(binding.SourcePort);
        Assert.True(binding.Drives);
        Assert.False(binding.CarriesTrigger);
        Assert.Equal(new ConsumptionKey(NodePath.Of("成员"), null), binding.Key);
    }

    [Fact]
    public void Gate_binding_joins_readiness_without_driving_versions()
    {
        InputBinding binding = new(
            NodePath.Of("整体"),
            BindingSource.ForNode(NodePath.Of("来源"), new PortName("结论")),
            new PortName("把关"),
            PortTypes.Text,
            Trigger: true,
            Role: BindingRole.Gate);

        Assert.False(binding.Drives);
        Assert.True(binding.CarriesTrigger);
    }

    [Fact]
    public void Static_binding_only_guards_startup()
    {
        InputBinding binding = new(
            NodePath.Of("节点"),
            BindingSource.ForNode(NodePath.Of("来源"), new PortName("结论")),
            new PortName("配置"),
            PortTypes.Text,
            Role: BindingRole.Static);

        Assert.False(binding.Drives);
        Assert.False(binding.CarriesTrigger);
    }

    [Fact]
    public void Group_drives_only_by_dynamic_bindings()
    {
        InputBinding member = InputBinding.Member(NodePath.Of("整体"), NodePath.Of("成员"));
        InputBinding gate = new(
            NodePath.Of("整体"),
            BindingSource.ForNode(NodePath.Of("把关"), new PortName("信号")),
            new PortName("把关"),
            PortTypes.Signal,
            Trigger: true,
            Role: BindingRole.Gate);
        InputGroup group = new([member, gate]);

        Assert.Equal([member], group.DrivingBindings);
        Assert.Empty(group.Signals);
        Assert.False(group.DrivenBySignals);
    }

    [Fact]
    public void Plan_groups_only_dynamic_bindings()
    {
        InputBinding driven = new(
            NodePath.Of("乙"),
            BindingSource.ForNode(NodePath.Of("甲"), new PortName("结果")),
            new PortName("目标"),
            PortTypes.Text,
            Group: 2);
        InputBinding gate = new(
            NodePath.Of("乙"),
            BindingSource.ForNode(NodePath.Of("把关"), new PortName("信号")),
            new PortName("把关"),
            PortTypes.Signal,
            Trigger: true,
            Role: BindingRole.Gate);

        NodeInputPlan plan = NodeInputPlan.Of(new ExecutableSpec(), [driven, gate], [], []);

        InputBinding planned = Assert.Single(Assert.Single(plan.Groups).Bindings);
        Assert.Equal(driven, planned);
        Assert.Equal(2, planned.Group);
    }

    [Fact]
    public void Container_plan_puts_members_and_gates_in_one_group()
    {
        ContainerNode container = new()
        {
            Path = NodePath.Of("整体"),
            Type = ContainerType(),
            Children =
            [
                new ExecutableNode
                {
                    Path = NodePath.Of("整体", "成员"),
                    Type = ContainerType(),
                    Spec = new ExecutableSpec(),
                },
            ],
            Inputs = [new InputPort(new PortName("把关"), PortTypes.Signal, mode: PortMode.Trigger)],
            InputBindings = new Dictionary<PortName, InputBinding>
            {
                [new PortName("把关")] = new(
                    NodePath.Of("整体"),
                    BindingSource.ForNode(NodePath.Of("来源"), new PortName("信号")),
                    new PortName("把关"),
                    PortTypes.Signal,
                    Trigger: true,
                    Role: BindingRole.Gate),
            },
        };

        NodeInputPlan plan = NodeInputPlan.Of(container);
        IReadOnlyList<InputBinding> bindings = Assert.Single(plan.Groups).Bindings;

        Assert.Equal(2, bindings.Count);
        Assert.Single(bindings, binding => binding.Drives);
        Assert.Single(bindings, binding => binding.Role == BindingRole.Gate);
    }

    private static StubNodeType ContainerType() =>
        new(new NodeKind("Container"), new NodeTypeShape { Organizes = true });

    private sealed class StubNodeType(NodeKind kind, NodeTypeShape shape) : INodeType
    {
        public NodeKind Kind { get; } = kind;

        public NodeTypeShape Shape { get; } = shape;
    }
}

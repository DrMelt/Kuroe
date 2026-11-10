using Kuroe.Flow.Runtime.Nodes;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Readiness;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Flow.Tests.Runtime.Nodes;

/// <summary>容器内容齐备只由内容来源决定，门控来源不撤销已发布快照。</summary>
public sealed class RuntimeContainerTests
{
    [Fact]
    public void Gate_release_does_not_change_published_content_readiness()
    {
        NodePath containerPath = NodePath.Of("整体");
        NodePath member = NodePath.Of("整体", "成员");
        NodePath gate = NodePath.Of("来源");
        ContainerNode container = new()
        {
            Path = containerPath,
            Type = TestNodeTypes.Container,
            Children = [new ExecutableNode { Path = member, Type = TestNodeTypes.Executable, Spec = new ExecutableSpec() }],
            InputBindings = new Dictionary<PortName, InputBinding>
            {
                [new PortName("把关")] = new(
                    containerPath,
                    BindingSource.ForNode(gate, new PortName("信号")),
                    new PortName("把关"),
                    PortTypes.Signal,
                    Role: BindingRole.Gate),
            },
        };
        StubContext context = new(NodeInputPlan.Of(container), containerPath, member, gate);
        RuntimeContainer runtime = new(container, context);

        Assert.True(runtime.ContentReady);

        runtime.Activate(context, NodeInputValues.Empty);
        Assert.True(runtime.Released);

        context.GateReleased = true;
        Assert.True(runtime.Released);

        context.GateReleased = false;
        Assert.True(runtime.Released);
    }

    private sealed class StubContext(
        NodeInputPlan plan,
        NodePath container,
        NodePath member,
        NodePath gate) : INodeContext
    {
        public bool GateReleased { get; set; }

        public NodeInputPlan? Plan(NodePath node) => node == container ? plan : null;

        public NodeRuntime Runtime(NodePath node) => throw new NotSupportedException();

        public NodeRuntime? Materialized(NodePath node) => null;

        public IReadOnlyList<ContextValue> OutputValues(BindingSource source) => [];

        public ISourceState Source(BindingSource source) =>
            new State(source.Node == member || source.Node == gate && GateReleased);
    }

    private sealed record State(bool Released) : ISourceState
    {
        public long Revision => 1;
    }
}
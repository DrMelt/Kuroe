using ErrorOr;
using Kuroe.Flow.NodeType.Container;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.NodeLibrary.Tests.Support;
using Xunit;

namespace Kuroe.Flow.NodeLibrary.Tests;

/// <summary>节点库展开：库容器的成员并入引用处，未知定义在展开期拒绝。</summary>
public sealed class NodeLibraryExpanderTests
{
    private static readonly NodeName Delivery = new("交付组");
    private static readonly NodeLabel Instance = new("交付");

    [Fact]
    public void Referenced_container_members_are_inlined_at_the_reference()
    {
        FlowDefinition flow = Flow(Instance, Delivery);

        ErrorOr<IReadOnlyList<FlowDefinition>> expanded = new NodeLibraryExpander()
            .Expand([flow], [], [Library()]);

        Assert.False(expanded.IsError,
            string.Join("；", expanded.ErrorsOrEmptyList.Select(error => error.Description)));
        NodeSpec root = Assert.Single(expanded.Value).RootNode;
        NodeSpec container = Assert.Single(root.Nodes!);
        Assert.Equal(Instance, container.Label);
        Assert.Equal(TestNodeTypes.ExecutableKind, Assert.Single(container.Nodes!).Kind);
    }

    [Fact]
    public void Unknown_reference_is_rejected()
    {
        FlowDefinition flow = Flow(new NodeLabel("缺失"), new NodeName("不存在"));

        ErrorOr<IReadOnlyList<FlowDefinition>> expanded = new NodeLibraryExpander().Expand([flow], [], []);

        Assert.True(expanded.IsError);
        Assert.Contains(expanded.ErrorsOrEmptyList, error => error.Description.Contains("不在节点库", StringComparison.Ordinal));
    }

    private static NodeSpec Library() => NodeSpec.Define(Delivery) with
    {
        Type = ContainerNodeTypes.Type,
        Nodes =
        [
            NodeSpec.Define(new NodeName("实施")) with
            {
                Type = TestNodeTypes.Executable,
                Spec = new ExecutableSpec(),
            },
        ],
    };

    private static FlowDefinition Flow(NodeLabel label, NodeName use) => new(
        new FlowName("默认"),
        null,
        [],
        new Dictionary<PortName, string>(),
        NodeSpec.Define(new NodeName("整体")) with
        {
            Type = ContainerNodeTypes.Type,
            Nodes = [NodeSpec.Reference(label, use)],
        });
}

using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Node.Command.Tests;

/// <summary>测试用的任务上下文：只给出节点的输入侧方案、来源状态与来源运行期对象。</summary>
internal sealed class StubNodeContext(ExecutableNode node) : INodeContext
{
    private readonly ExecutableNode _node = node;

    public INodeGraph Graph { get; } = new StubGraph(node);

    public NodeInputPlan? Plan(NodePath node) => node == _node.Path ? _node.Plan : null;

    public NodeRuntime Runtime(NodePath node) => throw new NotSupportedException();

    public NodeRuntime? Materialized(NodePath node) => null;

    public ISourceState Source(NodePath node) => new ReleasedSource();
}

/// <summary>测试用的来源状态：已放行，版本固定为 1。</summary>
internal sealed class ReleasedSource : ISourceState
{
    public bool Released => true;

    public long Revision => 1;
}

/// <summary>测试用的执行视图：只回答目标节点的入边与输入侧方案。</summary>
internal sealed class StubGraph(ExecutableNode node) : INodeGraph
{
    public IReadOnlyList<GraphNode> Nodes => [node];

    public StartNode Start => throw new NotSupportedException();

    public IReadOnlyList<ExecutableNode> ExecutableNodes => [node];

    public IReadOnlyList<ContainerNode> Containers => [];

    public IReadOnlyList<FlowEdge> Edges => node.Incoming;

    public int Count => 1;

    public int TotalExecutables => 1;

    public GraphNode this[NodePath path] => path == node.Path ? node : throw new NotSupportedException();

    public IReadOnlyList<GraphNode> ChildrenOf(NodePath? container) => [];

    public ContainerNode? ContainerOf(NodePath path) => null;

    public IReadOnlyList<FlowEdge> Incoming(NodePath target) => [.. node.Incoming.Where(edge => edge.Target == target)];

    public IReadOnlyList<FlowEdge> Outgoing(NodePath source) => [];

    public IReadOnlyList<NodePath> ExecutablesIn(NodePath source) => [node.Path];

    public NodeInputPlan? PlanOf(NodePath path) => node.Plan;

    public NodeTypeShape? ShapeOf(NodePath path) => node.Type.Shape;

    public PortType TypeOf(NodePath node, PortName port) => PortTypes.Text;
}
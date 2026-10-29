using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Readiness;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Node.Command.Tests;

/// <summary>测试用的任务上下文：只给出节点的输入侧方案、来源状态与来源运行期对象。</summary>
internal sealed class StubNodeContext(ExecutableNode node) : INodeContext
{
    private readonly ExecutableNode _node = node;
    private readonly Dictionary<NodePath, NodeRuntime> _sources = [];

    public INodeGraph Graph { get; } = new StubGraph(node);

    /// <summary>登记一个来源运行期对象，供节点读取输入内容。</summary>
    public void AddSource(NodeRuntime runtime) => _sources[runtime.Path] = runtime;

    public NodeInputPlan? Plan(NodePath node) => node == _node.Path ? _node.Plan : null;

    public NodeRuntime Runtime(NodePath node) => _sources[node];

    public NodeRuntime? Materialized(NodePath node) => _sources.GetValueOrDefault(node);

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

/// <summary>测试用的来源运行期对象：按输出端口给出固定文本。</summary>
internal sealed class SourceRuntime(NodePath path, string content)
    : NodeRuntime(new StartNode { Path = path, Outputs = [new Port(new PortName("结果"), PortTypes.Text)] })
{
    public override bool Released => true;

    public override long Revision => 1;

    public override bool Canceled => false;

    public override void Cancel()
    {
    }

    public override string? PortText(INodeContext context, PortName port) => content;

    public override IReadOnlyList<ContextValue> OutputValues(INodeContext context, PortName port) =>
        [new ContextValue(new StartSource(port), content)];
}
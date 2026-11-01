using Kuroe.Flow.Runtime.Nodes;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Readiness;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Node.File.Tests;

/// <summary>测试用的任务上下文：只给出节点的输入侧方案、来源状态与来源运行期对象。</summary>
internal sealed class StubNodeContext(ExecutableNode node) : INodeContext
{
    private readonly ExecutableNode _node = node;
    private readonly NodeInputPlan _plan = NodeInputPlan.Of(node.Spec, node.Incoming, node.Inputs, []);
    private readonly Dictionary<NodePath, NodeRuntime> _sources = [];

    public INodeGraph Graph { get; } = new StubGraph(node);

    /// <summary>登记一个来源运行期对象，供文件写入节点读取内容。</summary>
    public void AddSource(NodeRuntime runtime) => _sources[runtime.Path] = runtime;

    public NodeInputPlan? Plan(NodePath node) => node == _node.Path ? _plan : null;

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
    private readonly NodeInputPlan _plan = NodeInputPlan.Of(node.Spec, node.Incoming, node.Inputs, []);

    public IReadOnlyList<GraphNode> Nodes => [node];

    public StartNode Start => throw new NotSupportedException();

    public IReadOnlyList<ExecutableNode> ExecutableNodes => [node];

    public IReadOnlyList<ContainerNode> Containers => [];

    public IReadOnlyList<InputBinding> Bindings => node.Incoming;

    public IReadOnlyList<InputBinding> StaticBindings => [];

    public int Count => 1;

    public int TotalExecutables => 1;

    public GraphNode this[NodePath path] => path == node.Path ? node : throw new NotSupportedException();

    public IReadOnlyList<GraphNode> ChildrenOf(NodePath? container) => [];

    public ContainerNode? ContainerOf(NodePath path) => null;

    public IReadOnlyList<InputBinding> Incoming(NodePath target) =>
        [.. node.Incoming.Where(binding => binding.Target == target)];

    public IReadOnlyList<InputBinding> Outgoing(NodePath source) => [];

    public IReadOnlyList<NodePath> Dependents(NodePath source) => [];

    public StaticResolution? StaticInputResolution(NodePath node, PortName port) => null;

    public StaticResolution? ConstantResolution(NodePath node, PortName port) => null;

    public IReadOnlyList<NodePath> ExecutablesIn(NodePath source) => [node.Path];

    public NodeInputPlan? PlanOf(NodePath path) => _plan;

    public NodeTypeShape? ShapeOf(NodePath path) => node.Type.Shape;

    public PortType TypeOf(NodePath node, PortName port) => PortTypes.Text;
}

/// <summary>测试用的来源运行期对象：在结果端口发布固定文本。</summary>
internal sealed class SourceRuntime : RuntimeNode
{
    public SourceRuntime(NodePath path, string content)
        : base(new StartNode { Path = path, Outputs = [new Port(new PortName("结果"), PortTypes.Text)] })
    {
        Progress.Publish(new Publication(
            1,
            new Dictionary<PortName, bool>(),
            new Dictionary<PortName, IReadOnlyList<ContextValue>>
            {
                [new PortName("结果")] = [new ContextValue(new StartSource(new PortName("结果")), content)],
            }));
    }

    public override bool Canceled => false;

    protected override bool Consumable => true;

    public override void Cancel()
    {
    }

    /// <summary>测试来源没有需要持久化的状态。</summary>
    public override NodeCheckpoint? CaptureCheckpoint() => null;

    /// <summary>测试来源没有需要恢复的状态。</summary>
    public override void RestoreCheckpoint(NodeCheckpoint checkpoint, INodeRestoreContext context)
    {
    }
}
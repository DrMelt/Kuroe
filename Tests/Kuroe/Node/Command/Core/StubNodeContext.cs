using Kuroe.Flow.Runtime.Nodes;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Persistence.Records.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Readiness;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Node.Command.Tests;

/// <summary>测试用的任务上下文：只给出节点的输入侧方案、来源状态与来源运行期对象。</summary>
internal sealed class StubNodeContext(ExecutableNode node) : INodeContext
{
    private readonly ExecutableNode _node = node;
    private readonly NodeInputPlan _plan = NodeInputPlan.Of(node.Spec, node.Incoming, node.Inputs, []);
    private readonly Dictionary<NodePath, NodeRuntime> _sources = [];

    /// <summary>登记一个来源运行期对象，供节点读取输入内容。</summary>
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
    public override NodeRecord? CaptureRecord() => null;

    /// <summary>测试来源没有需要恢复的状态。</summary>
    public override void RestoreRecord(NodeRecord record, INodeRestoreContext context)
    {
    }
}
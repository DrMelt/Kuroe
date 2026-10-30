using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Readiness;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Kuroe.Flow.Runtime.Nodes;
using Kuroe.Flow.Compilation.Graph;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;
using Xunit;

namespace Kuroe.Flow.Tests;

/// <summary>输入侧就绪判定：组间任一齐备、组内全部齐备、按来源版本驱动、执行次数上限与强制重跑。
/// 判定是纯函数，来源侧按接口读数，不创建节点。</summary>
public sealed class ReadinessTests
{
    private static readonly PortName OutputPort = new("结论");

    /// <summary>来源有新版本才启动：已消费过的版本不再触发，来源再发布即重新就绪。</summary>
    [Fact]
    public void Version_driven_node_needs_a_new_source_version()
    {
        (NodeInputPlan plan, FakeSources sources, RuntimeExecutable node) = Single();

        Assert.False(plan.Ready(node, sources));

        sources.Of("节点1").Publish();
        Assert.True(plan.Ready(node, sources));

        node.Consume(plan, sources);
        Assert.False(plan.Ready(node, sources));

        sources.Of("节点1").Publish();
        Assert.True(plan.Ready(node, sources));
    }

    /// <summary>组间任一齐备即启动：另一组的数据来源尚未放行也能启动。</summary>
    [Fact]
    public void Any_ready_group_starts_the_node()
    {
        (NodeInputPlan plan, FakeSources sources, RuntimeExecutable node) = TwoGroups();

        sources.Of("节点2").Publish();

        Assert.True(plan.Ready(node, sources));
    }

    /// <summary>生效组取有驱动来源的齐备组，不是第一个齐备组。</summary>
    [Fact]
    public void Driven_group_returns_the_group_with_a_fresh_source()
    {
        (NodeInputPlan plan, FakeSources sources, RuntimeExecutable node) = TwoGroups();

        sources.Of("节点1").Publish();
        sources.Of("节点2").Publish();
        node.Consume(plan, sources);
        sources.Of("节点2").Publish();

        Assert.Equal(1, plan.DrivenGroup(node, sources));
    }

    /// <summary>强制重跑在没有任何齐备组时也能启动。</summary>
    [Fact]
    public void Forced_rerun_is_ready_without_a_driven_group()
    {
        (NodeInputPlan plan, FakeSources sources, RuntimeExecutable node) = TwoGroups();
        node.RequestRerun();

        Assert.Null(plan.DrivenGroup(node, sources));
        Assert.True(plan.Ready(node, sources));
    }

    /// <summary>组内必须全部齐备：缺一处时组不齐备，任何组都不成立即不启动。</summary>
    [Fact]
    public void Group_waits_for_every_binding()
    {
        (NodeInputPlan plan, FakeSources sources, RuntimeExecutable node) = OneGroupOfTwo();

        sources.Of("节点1").Publish();
        Assert.False(plan.Ready(node, sources));

        sources.Of("节点2").Publish();
        Assert.True(plan.Ready(node, sources));
    }

    /// <summary>组内有信号绑定时只认信号来源的新版本：数据来源再发布也不触发。</summary>
    [Fact]
    public void Signal_binding_drives_only_by_the_signal_source()
    {
        (NodeInputPlan plan, FakeSources sources, RuntimeExecutable node) = WithSignal();

        sources.Of("节点1").Publish();
        sources.Of("节点2").Publish();
        Assert.True(plan.Ready(node, sources));

        node.Consume(plan, sources);

        sources.Of("节点1").Publish();
        Assert.False(plan.Ready(node, sources));

        sources.Of("节点2").Publish();
        Assert.True(plan.Ready(node, sources));
    }

    /// <summary>达到执行次数上限后不再启动。</summary>
    [Fact]
    public void Execution_limit_stops_further_starts()
    {
        ExecutableNode target = Definition("节点2", maxExecutions: 1);
        NodeGraph graph = new([StartDefinition(), Definition("节点1"), target], [Value("节点1", "节点2", PortTypes.Text)]);
        NodeInputPlan plan = Plan(graph, "节点2");
        FakeSources sources = new();
        RuntimeExecutable node = new(target);

        sources.Of("节点1").Publish();
        Assert.True(plan.Ready(node, sources));

        node.RecordExecution();
        node.Consume(plan, sources);

        Assert.False(plan.Ready(node, sources));
    }

    /// <summary>返工作废后的重跑意图越过版本比较直接启动。</summary>
    [Fact]
    public void Forced_rerun_starts_without_a_new_version()
    {
        (NodeInputPlan plan, FakeSources sources, RuntimeExecutable node) = Single();

        sources.Of("节点1").Publish();
        node.Consume(plan, sources);
        Assert.False(plan.Ready(node, sources));

        node.RequestRerun();

        Assert.True(plan.Ready(node, sources));
    }

    /// <summary>已消费版本记录经接口注入，启动登记交给注入的实现。</summary>
    [Fact]
    public void Consume_uses_the_injected_store()
    {
        ExecutableNode target = Definition("节点2");
        NodeGraph graph = new([StartDefinition(), Definition("节点1"), target], [Value("节点1", "节点2", PortTypes.Text)]);
        NodeInputPlan plan = Plan(graph, "节点2");
        FakeSources sources = new();
        RecordingStore store = new();
        RuntimeExecutable node = new(target, consumed: store);

        sources.Of("节点1").Publish();
        node.Consume(plan, sources);

        Assert.Same(plan, store.Recorded);
    }

    /// <summary>一个来源到目标的单组场景。</summary>
    private static (NodeInputPlan Plan, FakeSources Sources, RuntimeExecutable Node) Single() =>
        Build([Value("节点1", "节点2", PortTypes.Text)]);

    /// <summary>同一目标上的两个组，各由不同的数据来源驱动。</summary>
    private static (NodeInputPlan Plan, FakeSources Sources, RuntimeExecutable Node) TwoGroups() =>
        Build([Value("节点1", "节点3", PortTypes.Text, group: 0), Value("节点2", "节点3", PortTypes.Text, group: 1)]);

    /// <summary>一个组内两处绑定，两处都放行才齐备。</summary>
    private static (NodeInputPlan Plan, FakeSources Sources, RuntimeExecutable Node) OneGroupOfTwo() =>
        Build([Value("节点1", "节点3", PortTypes.Text), Value("节点2", "节点3", PortTypes.Text)]);

    /// <summary>一个组内一个数据来源与一个信号来源。</summary>
    private static (NodeInputPlan Plan, FakeSources Sources, RuntimeExecutable Node) WithSignal() =>
        Build([Value("节点1", "节点2", PortTypes.Text), Value("节点2", "节点2", PortTypes.Signal, new PortName("信号"))]);

    /// <summary>按边建出图、方案与判定侧，目标节点取最后一条边的目标。</summary>
    private static (NodeInputPlan Plan, FakeSources Sources, RuntimeExecutable Node) Build(IReadOnlyList<FlowEdge> edges)
    {
        ExecutableNode target = Definition(edges[^1].Target.Value);
        List<ExecutableNode> others =
        [
            .. edges
                .SelectMany(edge => new[] { edge.Source, edge.Target })
                .Distinct()
                .Where(path => path != target.Path)
                .Select(path => Definition(path.Value)),
        ];
        NodeGraph graph = new([StartDefinition(), .. others, target], edges);

        return (Plan(graph, target.Path.Value), new FakeSources(), new RuntimeExecutable(target));
    }

    private static NodeInputPlan Plan(NodeGraph graph, string node) =>
        graph.PlanOf(NodePath.Of(node)) ?? throw new InvalidOperationException($"节点 {node} 没有输入侧方案。");

    private static FlowEdge Value(string from, string to, PortType type, PortName? input = null, int group = 0) =>
        new(NodePath.Of(from), NodePath.Of(to), OutputPort, input ?? OutputPort, type, group);

    private static ExecutableNode Definition(string name, int maxExecutions = 100) => new()
    {
        Path = NodePath.Of(name),
        Type = TestNodeTypes.Executable,
        Spec = new ExecutableSpec(),
        Inputs = [],
        Outputs = [new Port(OutputPort, PortTypes.Text)],
        MaxExecutions = maxExecutions,
    };

    private static StartNode StartDefinition() => new()
    {
        Path = new NodePath([StartNode.ReservedLabel]),
        Outputs = [new Port(OutputPort, PortTypes.Text)],
    };

    /// <summary>接收启动登记的已消费版本记录替身。</summary>
    private sealed class RecordingStore : IConsumedRevisionStore
    {
        public NodeInputPlan? Recorded { get; private set; }

        public long Revision(ConsumptionKey key) => 0;

        public void Record(InputGroup group, IReadinessSources sources)
        {
        }

        public void Record(NodeInputPlan plan, IReadinessSources sources) => Recorded = plan;

        public IReadOnlyList<ConsumedRevisionCheckpoint> CaptureCheckpoint() => [];

        public void RestoreCheckpoint(IReadOnlyList<ConsumedRevisionCheckpoint> consumed)
        {
        }
    }

    /// <summary>判定用的来源侧：按节点路径给出放行状态与版本，未登记时建一个未放行的。</summary>
    private sealed class FakeSources : IReadinessSources
    {
        private readonly Dictionary<NodePath, SourceState> _sources = [];

        public ISourceState Source(NodePath node) => Of(node.Value);

        /// <summary>取一个来源的状态，未登记时建一个未放行的。</summary>
        public SourceState Of(string node)
        {
            NodePath path = NodePath.Of(node);
            if (!_sources.TryGetValue(path, out SourceState? source))
            {
                source = new SourceState();
                _sources[path] = source;
            }

            return source;
        }

        /// <summary>一个来源的状态：是否放行与发布计数。</summary>
        internal sealed class SourceState : ISourceState
        {
            private long _revision;

            public bool Released { get; private set; }

            public long Revision => _revision;

            /// <summary>发布一次新版本。</summary>
            public void Publish()
            {
                Released = true;
                _revision++;
            }
        }
    }
}
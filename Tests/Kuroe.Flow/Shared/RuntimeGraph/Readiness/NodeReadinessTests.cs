using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Readiness;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Flow.Shared.Tests.RuntimeGraph.Readiness;

/// <summary>输入侧就绪判定：组间任一齐备、组内全部齐备、按来源版本驱动、执行次数上限与强制重跑。
/// 判定是纯函数，来源侧按接口读数，不创建节点。</summary>
public sealed class NodeReadinessTests
{
    private static readonly PortName OutputPort = new("结论");

    /// <summary>来源有新版本才启动：已消费过的版本不再触发，来源再发布即重新就绪。</summary>
    [Fact]
    public void Version_driven_node_needs_a_new_source_version()
    {
        (NodeInputPlan plan, FakeSources sources, FakeProgress node) = Single();

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
        (NodeInputPlan plan, FakeSources sources, FakeProgress node) = TwoGroups();

        sources.Of("节点2").Publish();

        Assert.True(plan.Ready(node, sources));
    }

    /// <summary>生效组取有驱动来源的齐备组，不是第一个齐备组。</summary>
    [Fact]
    public void Driven_group_returns_the_group_with_a_fresh_source()
    {
        (NodeInputPlan plan, FakeSources sources, FakeProgress node) = TwoGroups();

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
        (NodeInputPlan plan, FakeSources sources, FakeProgress node) = TwoGroups();
        node.RequestRerun();

        Assert.Null(plan.DrivenGroup(node, sources));
        Assert.True(plan.Ready(node, sources));
    }

    /// <summary>组内必须全部齐备：缺一处时组不齐备，任何组都不成立即不启动。</summary>
    [Fact]
    public void Group_waits_for_every_binding()
    {
        (NodeInputPlan plan, FakeSources sources, FakeProgress node) = OneGroupOfTwo();

        sources.Of("节点1").Publish();
        Assert.False(plan.Ready(node, sources));

        sources.Of("节点2").Publish();
        Assert.True(plan.Ready(node, sources));
    }

    /// <summary>组内有信号绑定时只认信号来源的新版本：数据来源再发布也不触发。</summary>
    [Fact]
    public void Signal_binding_drives_only_by_the_signal_source()
    {
        (NodeInputPlan plan, FakeSources sources, FakeProgress node) = WithSignal();

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
        NodeInputPlan plan = Plan([Value("节点1", "节点2", PortTypes.Text)], maxExecutions: 1);
        FakeSources sources = new();
        FakeProgress node = new();

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
        (NodeInputPlan plan, FakeSources sources, FakeProgress node) = Single();

        sources.Of("节点1").Publish();
        node.Consume(plan, sources);
        Assert.False(plan.Ready(node, sources));

        node.RequestRerun();

        Assert.True(plan.Ready(node, sources));
    }

    /// <summary>一个来源到目标的单组场景。</summary>
    private static (NodeInputPlan Plan, FakeSources Sources, FakeProgress Node) Single() =>
        Build([Value("节点1", "节点2", PortTypes.Text)]);

    /// <summary>同一目标上的两个组，各由不同的数据来源驱动。</summary>
    private static (NodeInputPlan Plan, FakeSources Sources, FakeProgress Node) TwoGroups() =>
        Build([Value("节点1", "节点3", PortTypes.Text, group: 0), Value("节点2", "节点3", PortTypes.Text, group: 1)]);

    /// <summary>一个组内两处绑定，两处都放行才齐备。</summary>
    private static (NodeInputPlan Plan, FakeSources Sources, FakeProgress Node) OneGroupOfTwo() =>
        Build([Value("节点1", "节点3", PortTypes.Text), Value("节点2", "节点3", PortTypes.Text)]);

    /// <summary>一个组内一个数据来源与一个信号来源。</summary>
    private static (NodeInputPlan Plan, FakeSources Sources, FakeProgress Node) WithSignal() =>
        Build([Value("节点1", "节点2", PortTypes.Text), Value("节点2", "节点2", PortTypes.Signal, new PortName("信号"))]);

    /// <summary>按边建出方案与判定侧。</summary>
    private static (NodeInputPlan Plan, FakeSources Sources, FakeProgress Node) Build(IReadOnlyList<FlowEdge> edges) =>
        (Plan(edges), new FakeSources(), new FakeProgress());

    /// <summary>按入边建立目标节点的输入侧方案。</summary>
    private static NodeInputPlan Plan(IReadOnlyList<FlowEdge> incoming, int maxExecutions = 100) =>
        NodeInputPlan.Of(new ExecutableSpec { MaxExecutions = maxExecutions }, incoming, [], []);

    private static FlowEdge Value(string from, string to, PortType type, PortName? input = null, int group = 0) =>
        new(NodePath.Of(from), NodePath.Of(to), OutputPort, input ?? OutputPort, type, group);

    /// <summary>判定要读的目标自身状态替身：已消费版本、执行次数、发布与重跑意图。</summary>
    private sealed class FakeProgress : IInputProgress
    {
        private readonly Dictionary<ConsumptionKey, long> _consumed = [];

        public int Executions { get; private set; }

        public bool Published { get; set; }

        public bool ForceRerun { get; private set; }

        public long ConsumedRevision(ConsumptionKey key) => _consumed.GetValueOrDefault(key);

        public void Consume(InputGroup group, IReadinessSources sources)
        {
            foreach (InputBinding binding in group.Bindings)
            {
                _consumed[binding.Key] = sources.Source(binding.Source).Revision;
            }
        }

        public void Consume(NodeInputPlan plan, IReadinessSources sources)
        {
            foreach (InputGroup group in plan.Groups)
            {
                Consume(group, sources);
            }
        }

        public void RecordExecution() => Executions++;

        public void RequestRerun() => ForceRerun = true;
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
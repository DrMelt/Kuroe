using Kuroe.Flow.Execution.Runs;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.RuntimeNodes;
using Kuroe.Flow.StaticGraph;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.ExecutableNode;
using Xunit;

namespace Kuroe.Flow.Tests;

/// <summary>输入侧就绪判定：组间任一齐备、组内全部齐备、整节点按版本驱动、按条目逐实例判定。
/// 判定是纯函数，来源侧按接口读数，不创建节点。</summary>
public sealed class ReadinessTests
{
    /// <summary>整节点执行节点的启动条件是来源有新版本：已消费过的版本不再触发，来源再发布即重新就绪。</summary>
    [Fact]
    public void Version_driven_node_needs_a_new_source_version()
    {
        ExecutableNode source = Definition("节点1", NodeMode.Single);
        ExecutableNode target = Definition("节点2", NodeMode.Single);
        NodeGraph graph = new([StartDefinition(), source, target], [Value("节点1", "节点2", Text, new PortName("结论"))]);
        NodeInputPlan plan = Plan(graph, "节点2");
        FakeSources sources = new();
        RuntimeExecutable node = new(target);

        Assert.Empty(NodeReadiness.Ready(plan, node, sources));

        sources.Of("节点1").Publish();
        Assert.Single(NodeReadiness.Ready(plan, node, sources));

        node.Consume(plan, sources);
        Assert.Empty(NodeReadiness.Ready(plan, node, sources));

        sources.Of("节点1").Publish();
        Assert.Single(NodeReadiness.Ready(plan, node, sources));
    }

    /// <summary>同源的数据边与信号边各自记账：数据按整套消费一次，信号按来源版本驱动，与两边在 From 里的声明顺序无关。</summary>
    [Fact]
    public void Same_source_data_and_signal_keep_independent_consumption()
    {
        foreach (bool signalFirst in new[] { true, false })
        {
            Run(signalFirst);
        }
    }

    private static void Run(bool signalFirst)
    {
        ExecutableNode source = Definition("节点1", NodeMode.PerItem);
        ExecutableNode target = Definition("节点2", NodeMode.Single);
        FlowEdge data = new(NodePath.Of("节点1"), NodePath.Of("节点2"), EdgeKind.Value, FeedScope.AllInstances, FeedRecurrence.Once, OutputPort, new PortName("结论"), Text);
        FlowEdge trigger = new(NodePath.Of("节点1"), NodePath.Of("节点2"), EdgeKind.Value, FeedScope.Whole, FeedRecurrence.Repeat, OutputPort, new PortName("信号"), Signal);
        NodeGraph graph = new([StartDefinition(), source, target], signalFirst ? [trigger, data] : [data, trigger]);
        NodeInputPlan plan = Plan(graph, "节点2");
        FakeSources sources = new();
        RuntimeExecutable node = new(target);

        sources.Of("节点1").Publish([0, 1]);
        Assert.Single(NodeReadiness.Ready(plan, node, sources));

        node.Consume(plan, sources);
        Assert.Empty(NodeReadiness.Ready(plan, node, sources));

        sources.Of("节点1").Publish([0, 1]);
        Assert.Single(NodeReadiness.Ready(plan, node, sources));
    }

    /// <summary>按条目展开的执行节点组间任一齐备即启动：只管后一个数据来源的一组齐备就能启动全部条目，另一组的数据来源尚未放行。</summary>
    [Fact]
    public void PerItem_node_starts_when_any_group_is_ready()
    {
        Loop loop = Grouped();
        loop.Sources.Split("节点1", [0, 1]);
        loop.Sources.Of("节点3").Publish();

        Assert.Equal([0, 1], NodeReadiness.Ready(loop.Input, loop.Node, loop.Sources));
    }

    /// <summary>组内必须全部齐备：每个组都缺一处时一个条目都不启动。</summary>
    [Fact]
    public void PerItem_group_waits_for_every_binding()
    {
        Loop loop = Grouped();
        loop.Sources.Split("节点1", [0, 1]);

        Assert.Empty(NodeReadiness.Ready(loop.Input, loop.Node, loop.Sources));
    }

    /// <summary>两组同时齐备只启动一个实例：启动即登记方案内全部来源版本，另一组不再触发。</summary>
    [Fact]
    public void Two_ready_groups_start_once()
    {
        ExecutableNode first = Definition("节点1", NodeMode.Single);
        ExecutableNode second = Definition("节点2", NodeMode.Single);
        ExecutableNode target = Definition("节点3", NodeMode.Single);
        NodeGraph graph = new(
            [StartDefinition(), first, second, target],
            [
                Value("节点1", "节点3", Text, new PortName("甲"), group: 0),
                Value("节点2", "节点3", Text, new PortName("乙"), group: 1),
            ]);
        NodeInputPlan plan = Plan(graph, "节点3");
        FakeSources sources = new();
        RuntimeExecutable node = new(target);

        sources.Of("节点1").Publish();
        sources.Of("节点2").Publish();
        Assert.Single(NodeReadiness.Ready(plan, node, sources));

        node.Consume(plan, sources);
        Assert.Empty(NodeReadiness.Ready(plan, node, sources));
    }

    /// <summary>按条目展开的执行节点逐实例判定：已有产出的条目不再启动，其余条目照常启动。</summary>
    [Fact]
    public void Produced_instance_is_not_started_again()
    {
        Loop loop = Grouped();
        loop.Sources.Split("节点1", [0, 1]);
        loop.Sources.Of("节点3").Publish();
        loop.Node.AdoptItems([0, 1], null);
        loop.Node.Publish(Publishing(loop.Node, 0));

        Assert.Equal([1], NodeReadiness.Ready(loop.Input, loop.Node, loop.Sources));
    }

    /// <summary>造一次交回某条目的执行，把该条目标记为已产出。</summary>
    private static Run Publishing(RuntimeExecutable node, int item) => new(new RunId(1), new RunContext
    {
        Task = new TaskId(1),
        Node = node.Path,
        Instruction = "产出",
        Type = TestNodeTypes.Executable,
        ItemIndex = item,
    });

    /// <summary>拆分来源尚未交回时没有实例，任何组齐备都不启动。</summary>
    [Fact]
    public void Item_node_without_split_has_no_instance()
    {
        Loop loop = Grouped();
        loop.Sources.Of("节点3").Publish();

        Assert.Empty(NodeReadiness.Ready(loop.Input, loop.Node, loop.Sources));
    }

    /// <summary>对齐边的实例集取对齐来源已展开的实例，来源未产出的条目等它产出。</summary>
    [Fact]
    public void Aligned_binding_waits_for_the_source_instance()
    {
        ExecutableNode source = Definition("节点1", NodeMode.PerItem);
        ExecutableNode target = Definition("节点2", NodeMode.PerItem);
        NodeGraph graph = new([StartDefinition(), source, target], [Aligned("节点1", "节点2", OutputPort)]);
        NodeInputPlan plan = Plan(graph, "节点2");
        FakeSources sources = new();
        RuntimeExecutable node = new(target);

        sources.Of("节点1").Publish([0, 1]);
        sources.Of("节点1").Produce(0);

        Assert.Equal([0], NodeReadiness.Ready(plan, node, sources));
    }

    /// <summary>对齐来源只覆盖部分条目：来源不负责的条目视为齐备，负责的条目等它产出。</summary>
    [Fact]
    public void Aligned_source_not_covering_an_item_leaves_it_ready()
    {
        ExecutableNode split = Definition("节点1", NodeMode.Single, outputs: [new Port(new PortName("拆分"), PortType.WorkItems)]);
        ExecutableNode aligned = Definition("节点2", NodeMode.PerItem);
        ExecutableNode target = Definition("节点3", NodeMode.PerItem);
        NodeGraph graph = new(
            [StartDefinition(), split, aligned, target],
            [
                FlowEdge.Split(NodePath.Of("节点1"), NodePath.Of("节点3"), new PortName("拆分"), new PortName("任务")),
                Aligned("节点2", "节点3", new PortName("依据")),
            ]);
        NodeInputPlan plan = Plan(graph, "节点3");
        FakeSources sources = new();
        RuntimeExecutable node = new(target);

        sources.Split("节点1", [0, 1]);
        sources.Of("节点2").Publish([0]);

        Assert.Equal([1], NodeReadiness.Ready(plan, node, sources));

        sources.Of("节点2").Produce(0);
        Assert.Equal([0, 1], NodeReadiness.Ready(plan, node, sources));
    }

    /// <summary>两个组的按条目执行节点：空间取自拆分来源，组一要求拆分与数据来源，组二要求另一个数据来源。</summary>
    private static Loop Grouped()
    {
        ExecutableNode split = Definition("节点1", NodeMode.Single, outputs: [new Port(new PortName("拆分"), PortType.WorkItems)]);
        ExecutableNode first = Definition("节点2", NodeMode.Single);
        ExecutableNode second = Definition("节点3", NodeMode.Single);
        ExecutableNode target = Definition("节点4", NodeMode.PerItem);
        NodeGraph graph = new(
            [StartDefinition(), split, first, second, target],
            [
                FlowEdge.Split(NodePath.Of("节点1"), NodePath.Of("节点4"), new PortName("拆分"), new PortName("任务"), 0),
                Value("节点2", "节点4", Text, new PortName("结论"), group: 0),
                Value("节点3", "节点4", Text, new PortName("依据"), group: 1),
            ]);

        return new Loop(Plan(graph, "节点4"), new RuntimeExecutable(target), new FakeSources());
    }

    private readonly record struct Loop(NodeInputPlan Input, RuntimeExecutable Node, FakeSources Sources);

    private static NodeInputPlan Plan(NodeGraph graph, string node) =>
        graph.PlanOf(NodePath.Of(node)) ?? throw new InvalidOperationException($"节点 {node} 没有输入侧方案。");

    private static FlowEdge Value(string from, string to, PortType type, PortName input, int group = 0) =>
        new(NodePath.Of(from), NodePath.Of(to), EdgeKind.Value, FeedScope.Whole, FeedRecurrence.Repeat, OutputPort, input, type, group);

    private static FlowEdge Aligned(string from, string to, PortName input) =>
        new(NodePath.Of(from), NodePath.Of(to), EdgeKind.Value, FeedScope.Item, FeedRecurrence.Repeat, OutputPort, input, Text);

    private static ExecutableNode Definition(string name, NodeMode mode, IReadOnlyList<Port>? outputs = null) => new()
    {
        Path = NodePath.Of(name),
        Type = TestNodeTypes.Executable,
        Execution = new ExecutableSpec { Mode = mode },
        Model = new ModelDefinition { Name = new ModelRef("执行者") },
        Outputs = outputs ?? [],
        MaxRuns = 100,
    };

    private static StartNode StartDefinition() => new()
    {
        Path = new NodePath([StartNode.ReservedName]),
        Outputs = [new Port(OutputPort, PortType.Text)],
    };

    /// <summary>判定用的来源侧：按节点路径给出放行状态、版本、展开实例与产出，拆分另按节点路径登记。</summary>
    private sealed class FakeSources : IExecutionSources
    {
        private readonly Dictionary<NodePath, Source> _sources = [];
        private readonly Dictionary<NodePath, IReadOnlyList<int>> _splits = [];

        public ISourceState Source(NodePath node) => Of(node);

        public bool HasSplit(NodePath node) => _splits.ContainsKey(node);

        public IReadOnlyList<int>? SplitItems(NodePath node, BranchName? branch) =>
            _splits.TryGetValue(node, out IReadOnlyList<int>? items) ? items : null;

        public IReadOnlyList<int>? Items(NodePath node) => Of(node).Items;

        /// <summary>登记某来源已交回的条目序。</summary>
        public void Split(string node, IReadOnlyList<int> items) => _splits[NodePath.Of(node)] = items;

        /// <summary>取一个来源的状态，未登记时建一个未放行的。</summary>
        public Source Of(NodePath node)
        {
            if (!_sources.TryGetValue(node, out Source? source))
            {
                source = new Source();
                _sources[node] = source;
            }

            return source;
        }

        /// <summary>按节点名取一个来源的状态。</summary>
        public Source Of(string node) => Of(NodePath.Of(node));
    }

    /// <summary>来源的一刻状态：放行、版本、展开实例与已产出实例。</summary>
    private sealed class Source : ISourceState
    {
        private readonly HashSet<int> _produced = [];

        public bool Released { get; private set; }

        public long Revision { get; private set; }

        /// <summary>已展开的实例集，没有实例集的来源为空。</summary>
        public IReadOnlyList<int>? Items { get; private set; }

        public bool ProducedItem(int item) => _produced.Contains(item);

        /// <summary>发布一份新版本：放行并递增版本，给出实例集时记下展开的实例集。</summary>
        public void Publish(IReadOnlyList<int>? items = null)
        {
            Released = true;
            Revision++;
            _produced.Clear();
            Items = items is null ? null : [.. items];
        }

        /// <summary>登记该来源已产出的实例。</summary>
        public void Produce(int item) => _produced.Add(item);
    }

    private static readonly PortName OutputPort = new("结论");

    private const PortType Text = PortType.Text;

    private const PortType Signal = PortType.Signal;
}

using Kuroe.Flow.Graph.Shared.StaticGraph;
using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Readiness;

using Kuroe.FlowDescription.Definition.Shared.Ports;
using Kuroe.Kernel.Tasks.Shared;
using Xunit;

namespace Kuroe.Flow.Control.Tests.Runtime.Graph;

/// <summary>输入侧进展：消费版本、发布版本、作废与执行计数。</summary>
public sealed class InputProgressTests
{
    [Fact(DisplayName = "消费记录每个来源的当前修订")]
    public void Consume_records_the_current_revision_of_every_source()
    {
        NodePath source = NodePath.Of("来源");
        PortName port = new("结论");
        ConsumptionKey key = new(source, port);
        NodeInputPlan plan = new()
        {
            Groups =
            [
                new InputGroup(
                [
                    new InputBinding(
                        NodePath.Of("目标"),
                        BindingSource.ForNode(source, port),
                        new PortName("目标"),
                        PortTypes.Text),
                ]),
            ],
        };
        Sources sources = new() { Revision = 3 };
        InputProgress progress = new(TimeProvider.System);

        Assert.Equal(0, progress.ConsumedRevision(key));

        progress.Consume(plan.Groups[0], sources);
        Assert.Equal(3, progress.ConsumedRevision(key));

        sources.Revision = 5;
        Assert.Equal(3, progress.ConsumedRevision(key));
        Assert.Equal(0, progress.ConsumedRevision(new ConsumptionKey(NodePath.Of("未登记"), port)));
        Assert.Equal(0, progress.ConsumedRevision(new ConsumptionKey(source, new PortName("另一端口"))));
    }

    [Fact(DisplayName = "发布设置修订失效保留修订")]
    public void Publish_sets_the_revision_and_invalidate_keeps_it()
    {
        InputProgress progress = new(TimeProvider.System);
        Publication publication = new(
            2,
            new Dictionary<PortName, bool>(),
            new Dictionary<PortName, IReadOnlyList<ContextValue>>());

        progress.Publish(publication);

        Assert.True(progress.Published);
        Assert.Equal(2, progress.Revision);
        Assert.Equal(3, progress.NextRevision());

        progress.Invalidate();

        Assert.False(progress.Published);
        Assert.Equal(2, progress.Revision);
    }

    [Fact(DisplayName = "发布历史保留已失效的发布")]
    public void Publish_history_keeps_invalidated_publications()
    {
        StubTimeProvider time = new(TestTime.At);
        InputProgress progress = new(time);
        Publication first = new(
            1,
            new Dictionary<PortName, bool>(),
            new Dictionary<PortName, IReadOnlyList<ContextValue>>());
        Publication second = new(
            2,
            new Dictionary<PortName, bool>(),
            new Dictionary<PortName, IReadOnlyList<ContextValue>>());

        progress.Publish(first);
        progress.Invalidate();
        time.Set(TestTime.At.AddMinutes(1));
        progress.Publish(second);

        Assert.Equal([1L, 2L], progress.History.Select(entry => entry.Publication.Revision));
        Assert.Equal([TestTime.At, TestTime.At.AddMinutes(1)], progress.History.Select(entry => entry.At));
        Assert.Same(second, progress.Publication);
    }

    [Fact(DisplayName = "已开始计数执行且重跑被消费")]
    public void Started_counts_executions_and_rerun_is_consumed()
    {
        InputProgress progress = new(TimeProvider.System);

        progress.Started();
        progress.Started();
        progress.RequestRerun();
        progress.RecordGroup(2);

        Assert.Equal(2, progress.Executions);
        Assert.Equal(2, progress.LastInputGroup);
        Assert.True(progress.ForceRerun);

        progress.ClearRerun();

        Assert.False(progress.ForceRerun);
    }

    private sealed class Sources : IReadinessSources
    {
        public long Revision { get; set; }

        public ISourceState Source(BindingSource source) => new State(Revision);

        private sealed record State(long Revision) : ISourceState
        {
            public bool Released => true;
        }
    }
}

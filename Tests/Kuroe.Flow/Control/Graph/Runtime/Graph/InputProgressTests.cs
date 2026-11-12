using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Readiness;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Flow.Tests.Runtime.Graph;

/// <summary>输入侧进展：消费版本、发布版本、作废与执行计数。</summary>
public sealed class InputProgressTests
{
    [Fact]
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
        InputProgress progress = new();

        Assert.Equal(0, progress.ConsumedRevision(key));

        progress.Consume(plan.Groups[0], sources);
        Assert.Equal(3, progress.ConsumedRevision(key));

        sources.Revision = 5;
        Assert.Equal(3, progress.ConsumedRevision(key));
        Assert.Equal(0, progress.ConsumedRevision(new ConsumptionKey(NodePath.Of("未登记"), port)));
        Assert.Equal(0, progress.ConsumedRevision(new ConsumptionKey(source, new PortName("另一端口"))));
    }

    [Fact]
    public void Publish_sets_the_revision_and_invalidate_keeps_it()
    {
        InputProgress progress = new();
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

    [Fact]
    public void Publish_history_keeps_invalidated_publications()
    {
        InputProgress progress = new();
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
        progress.Publish(second);

        Assert.Equal([1L, 2L], progress.History.Select(entry => entry.Publication.Revision));
        Assert.Same(second, progress.Publication);
    }

    [Fact]
    public void Started_counts_executions_and_rerun_is_consumed()
    {
        InputProgress progress = new();

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

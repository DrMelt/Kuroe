using Kuroe.Flow.Runtime.Graph;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Readiness;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Flow.Tests;

/// <summary>已消费版本记录：按来源记当前版本，未登记的来源为零。</summary>
public sealed class ConsumedRevisionStoreTests
{
    [Fact]
    public void Record_stores_the_current_revision_of_every_source()
    {
        NodePath source = NodePath.Of("来源");
        PortName port = new("结论");
        ConsumptionKey key = new(source, port);
        NodeInputPlan plan = new()
        {
            Groups = [new InputGroup([new InputBinding(source, port, PortTypes.Text)])],
        };
        Sources sources = new() { Revision = 3 };
        ConsumedRevisionStore store = new();

        Assert.Equal(0, store.Revision(key));

        store.Record(plan, sources);
        Assert.Equal(3, store.Revision(key));

        sources.Revision = 5;
        Assert.Equal(3, store.Revision(key));
        Assert.Equal(0, store.Revision(new ConsumptionKey(NodePath.Of("未登记"), port)));
        Assert.Equal(0, store.Revision(new ConsumptionKey(source, new PortName("另一端口"))));
    }

    private sealed class Sources : IReadinessSources
    {
        public long Revision { get; set; }

        public ISourceState Source(NodePath node) => new State(Revision);

        private sealed record State(long Revision) : ISourceState
        {
            public bool Released => true;
        }
    }
}
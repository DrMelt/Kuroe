using Kuroe.Flow.Runtime.Graph;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Xunit;

namespace Kuroe.Flow.Tests;

/// <summary>消费账：按来源记当前版本，未登记的来源为零。</summary>
public sealed class ConsumptionLedgerTests
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
        ConsumptionLedger ledger = new();

        Assert.Equal(0, ledger.Revision(key));

        ledger.Record(plan, sources);
        Assert.Equal(3, ledger.Revision(key));

        sources.Revision = 5;
        Assert.Equal(3, ledger.Revision(key));
        Assert.Equal(0, ledger.Revision(new ConsumptionKey(NodePath.Of("未登记"), port)));
        Assert.Equal(0, ledger.Revision(new ConsumptionKey(source, new PortName("另一端口"))));
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
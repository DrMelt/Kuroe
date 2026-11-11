using Kuroe.Flow.Graph.Records;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Readiness;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Flow.Graph.Records.Shared.Tests;

/// <summary>输入侧进展的公共存档转换：版本、次数、启动组、消费、重跑与发布一次往返。</summary>
public sealed class InputProgressRecordsTests
{
    [Fact]
    public void Capture_and_restore_preserve_input_progress()
    {
        NodePath source = NodePath.Of("来源");
        PortName port = new("结论");
        ConsumptionKey key = new(source, port);
        InputProgress original = new();
        original.Started();
        original.RecordGroup(2);
        original.RequestRerun();
        original.Consume(
            new InputGroup([new InputBinding(
                NodePath.Of("目标"),
                BindingSource.ForNode(source, port),
                new PortName("目标"),
                PortTypes.Text)]),
            new Sources(4));
        original.Publish(new Publication(
            3,
            new Dictionary<PortName, bool> { [port] = false },
            new Dictionary<PortName, IReadOnlyList<ContextValue>>
            {
                [port] = [new ContextValue(new NodeSource(NodePath.Of("来源"), port), "内容")],
            }));

        NodePath node = NodePath.Of("来源");
        NodeCommonRecord record = InputProgressRecords.Capture(original);
        InputProgress restored = new();
        InputProgressRecords.RestorePorts(restored, PortPublicationRecords.Capture(node, original.History));
        InputProgressRecords.Restore(restored, record);

        Assert.Equal(3, restored.Revision);
        Assert.Equal(1, restored.Executions);
        Assert.Equal(2, restored.LastInputGroup);
        Assert.True(restored.ForceRerun);
        Assert.Equal(4, restored.ConsumedRevision(key));
        Assert.Single(restored.History);
        Assert.NotNull(restored.Publication);
        Assert.False(restored.Publication.Triggers[port]);
        Assert.Equal("内容", Assert.Single(restored.Publication.Output(port)).Text);
    }

    private sealed class Sources(long revision) : IReadinessSources
    {
        public ISourceState Source(BindingSource source) => new State(revision);

        private sealed record State(long Revision) : ISourceState
        {
            public bool Released => true;
        }
    }
}
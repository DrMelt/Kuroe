using Kuroe.Execution.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Tasks.Shared;
using Xunit;

namespace Kuroe.Flow.Graph.Records.Shared.Tests;

/// <summary>图记录的目标标识与来源标识往返。</summary>
public sealed class GraphRecordConversionTests
{
    [Fact]
    public void Target_round_trips_node_path()
    {
        NodePath path = NodePath.Of("整体", "计划/复核");

        NodePath parsed = GraphRecordTargets.Parse(GraphRecordTargets.Format(path));

        Assert.Equal(path, parsed);
    }

    [Fact]
    public void Execution_source_round_trips()
    {
        ContextSource source = new ExecutionSource(new ExecutionId(3), NodePath.Of("整体", "计划"), new PortName("结论"));

        ContextSource parsed = GraphRecordSources.Read(GraphRecordSources.Write(source));

        Assert.Equal(source, parsed);
    }

    [Fact]
    public void Start_source_round_trips()
    {
        ContextSource source = new StartSource(new PortName("目标"));

        ContextSource parsed = GraphRecordSources.Read(GraphRecordSources.Write(source));

        Assert.Equal(source, parsed);
    }

    [Fact]
    public void Invalid_source_is_rejected()
    {
        Assert.Throws<FormatException>(() => GraphRecordSources.Read("unknown:1:2:3"));
        Assert.Throws<FormatException>(() => GraphRecordSources.Read("start"));
    }
}
using Kuroe.FlowDescription.Shared.Model;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Execution;
using Kuroe.Tasks.Shared;
using Xunit;
using Kuroe.Execution.Shared.Identity;

namespace Kuroe.Flow.Records.Shared.Tests;

/// <summary>图记录的步骤标识与来源标识往返。</summary>
public sealed class GraphRecordConversionTests
{
    [Fact(DisplayName = "目标往返节点路径")]
    public void Target_round_trips_node_path()
    {
        NodePath path = NodePath.Of("整体", "计划/复核");

        NodePath parsed = GraphRecordSteps.Parse(GraphRecordSteps.Format(path));

        Assert.Equal(path, parsed);
    }

    [Fact(DisplayName = "执行来源往返")]
    public void Execution_source_round_trips()
    {
        ContextSource source = new ExecutionSource(new ExecutionId(3), NodePath.Of("整体", "计划"), new PortName("结论"));

        ContextSource parsed = GraphRecordSources.Read(GraphRecordSources.Write(source));

        Assert.Equal(source, parsed);
    }

    [Fact(DisplayName = "开始来源往返")]
    public void Start_source_round_trips()
    {
        ContextSource source = new StartSource(new PortName("目标"));

        ContextSource parsed = GraphRecordSources.Read(GraphRecordSources.Write(source));

        Assert.Equal(source, parsed);
    }

    [Fact(DisplayName = "非法来源被拒绝")]
    public void Invalid_source_is_rejected()
    {
        Assert.Throws<FormatException>(() => GraphRecordSources.Read("unknown:1:2:3"));
        Assert.Throws<FormatException>(() => GraphRecordSources.Read("start"));
    }
}
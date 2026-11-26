using Kuroe.FlowDescription.Definition.Shared.Flow;
using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.Kernel.Tasks.Shared;
using Xunit;

namespace Kuroe.FlowDescription.Semantics.Shared.Tests;

/// <summary>语义契约：来源终点的静态性、缺失性与解析结果的有效性。</summary>
public sealed class FlowSourceTests
{
    [Fact(DisplayName = "装配期来源标记为静态")]
    public void Literal_sources_are_static()
    {
        PortName port = new("端口");
        NodePath node = NodePath.Of("节点");

        Assert.True(FlowSource.StartFixed(port, "值").IsStatic);
        Assert.True(FlowSource.StartInput(port).IsStatic);
        Assert.True(FlowSource.NodeConstant(node, port, "值").IsStatic);
        Assert.True(FlowSource.Default("值").IsStatic);
        Assert.True(FlowSource.RuntimeConstant(node, port).IsStatic);
    }

    [Fact(DisplayName = "节点产出不是静态来源")]
    public void Node_output_is_not_static()
    {
        FlowSource source = FlowSource.NodeOutput(NodePath.Of("节点"), new PortName("端口"));

        Assert.False(source.IsStatic);
        Assert.False(source.IsMissing);
    }

    [Fact(DisplayName = "没有诊断的解析结果合法")]
    public void Semantics_without_diagnostics_is_valid()
    {
        FlowDefinition flow = new(
            new FlowName("流程"),
            null,
            [],
            new NodeSpec { Label = new NodeLabel("整体") });

        FlowSemantics semantics = new(flow, [], []);

        Assert.True(semantics.IsValid);
        Assert.Empty(semantics.Executables);
    }
}
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Xunit;

namespace Kuroe.Flow.Shared.Tests.RuntimeGraph.Execution;

/// <summary>内容出处：可读文本与回跳执行。</summary>
public sealed class ContextSourceTests
{
    [Fact]
    public void ExecutionSource_points_back_to_its_execution()
    {
        ExecutionId execution = new(7);
        ExecutionSource source = new(execution, NodePath.Of("实施"), new PortName("结论"));

        Assert.Equal("执行 #7 · 实施 产出", source.Label);
        Assert.Equal(execution, source.FromExecution);
        Assert.Equal(new PortName("结论"), source.Port);
    }

    [Fact]
    public void Input_and_start_sources_name_their_origin()
    {
        InputSource input = new(NodePath.Of("取舍"), new PortName("回答"));
        StartSource start = new(new PortName("目标"));

        Assert.Equal("节点「取舍」的输入", input.Label);
        Assert.Equal("流程起点端口「目标」", start.Label);
        Assert.Null(input.FromExecution);
        Assert.Null(start.FromExecution);
    }

    [Fact]
    public void Context_value_keeps_origin_and_text()
    {
        ContextValue value = new(new StartSource(new PortName("目标")), "做出一个示例");

        Assert.Equal("做出一个示例", value.Text);
        Assert.Equal("流程起点端口「目标」", value.Origin.Label);
    }

    [Fact]
    public void Container_port_source_names_its_container()
    {
        ContainerPortSource container = new(NodePath.Of("文档组"), new PortName("结论"));

        Assert.Equal("容器「文档组」端口「结论」", container.Label);
        Assert.Null(container.FromExecution);
    }
}

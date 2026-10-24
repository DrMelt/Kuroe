using Kuroe.Flow.Compiler.Shared;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Xunit;

namespace Kuroe.Flow.Compiler.Tests;

/// <summary>节点接口规则：端口类型落在类型值域，类型未注册时跳过值域检查，默认值只用于派发执行的 Text 输入。</summary>
public sealed class NodeSpecInterfaceTests
{
    [Fact]
    public void Input_port_type_outside_the_shape_is_rejected()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Spec = new ExecutableSpec(),
            Inputs = [new InputPort(new PortName("背景"), PortTypes.Context)],
        };

        Assert.Equal(["输入端口 背景 的类型只能是 Text。"], Errors(node, Shape(input: [PortTypes.Text])));
    }

    [Fact]
    public void Unknown_shape_skips_port_type_domain()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Spec = new ExecutableSpec(),
            Inputs = [new InputPort(new PortName("背景"), PortTypes.Context)],
        };

        Assert.Empty(Errors(node, shape: null));
    }

    [Fact]
    public void Default_value_is_rejected_on_context_input()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Spec = new ExecutableSpec(),
            Inputs = [new InputPort(new PortName("背景"), PortTypes.Context, null, "默认")],
        };

        Assert.Equal(
            ["输入端口 背景 的默认值只用于派发执行的节点的 Text 输入端口。"],
            Errors(node, Shape()));
    }

    private static NodeTypeShape Shape(IReadOnlyList<PortType>? input = null) => new()
    {
        DispatchesExecutions = true,
        InputTypes = input ?? [PortTypes.Text, PortTypes.Context, PortTypes.Signal],
        OutputTypes = [PortTypes.Text, PortTypes.Context, PortTypes.Signal],
    };

    private static List<string> Errors(NodeSpec node, NodeTypeShape? shape)
    {
        List<string> errors = [];
        node.CheckInterface(shape, errors.Add);

        return errors;
    }
}
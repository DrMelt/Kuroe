using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Flow.Compilation.Shared.Tests.Validation;

/// <summary>From 接线规则：动态输入由 From 覆盖，容器只有一组。</summary>
public sealed class NodeSpecRulesFromTests
{
    private static readonly PortName Input = new("输入");
    private static readonly PortName Output = new("输出");

    [Fact]
    public void Container_dynamic_input_requires_from()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("组")) with
        {
            Nodes = [NodeSpec.Define(new NodeName("成员"))],
            Inputs = [new InputPort(Input)],
        };

        Assert.Equal(
            ["容器声明动态输入端口时必须给出一组 From。"],
            Errors(fail => node.CheckFrom(fail)));
    }

    [Fact]
    public void Container_accepts_one_from_group_for_dynamic_inputs()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("组")) with
        {
            Nodes = [NodeSpec.Define(new NodeName("成员"))],
            Inputs = [new InputPort(Input)],
            From = [new FromGroup([new FromLink(Input, PortRef.Named(new NodeLabel("来源"), Output))])],
        };

        Assert.Empty(Errors(fail => node.CheckFrom(fail)));
    }

    [Fact]
    public void Container_rejects_multiple_from_groups()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("组")) with
        {
            Nodes = [NodeSpec.Define(new NodeName("成员"))],
            Inputs = [new InputPort(Input)],
            From =
            [
                new FromGroup([new FromLink(Input, PortRef.Named(new NodeLabel("甲"), Output))]),
                new FromGroup([new FromLink(Input, PortRef.Named(new NodeLabel("乙"), Output))]),
            ],
        };

        Assert.Contains(
            "容器只能写一组 From",
            Errors(fail => node.CheckFrom(fail)).Single());
    }

    private static List<string> Errors(Action<ValidationMessageHandler> check)
    {
        List<string> errors = [];
        check(errors.Add);

        return errors;
    }
}
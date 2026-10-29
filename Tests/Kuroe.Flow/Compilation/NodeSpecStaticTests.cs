using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Flow.Compilation.Tests;

/// <summary>节点静态端口规则：绑定目标是静态输入，静态输入有绑定或默认值。</summary>
public sealed class NodeSpecStaticTests
{
    private static readonly PortName Input = new("输入");
    private static readonly PortName Output = new("输出");

    [Fact]
    public void Static_binding_target_must_be_a_static_input()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Spec = new ExecutableSpec(),
            Inputs = [new InputPort(Input)],
            Static = new Dictionary<PortName, PortRef>
            {
                [Input] = PortRef.Named(new NodeLabel("来源"), Output),
            },
        };

        Assert.Equal(
            ["静态绑定目标 输入 不是已声明的静态输入端口。"],
            Errors(node.CheckStatic));
    }

    [Fact]
    public void Static_input_requires_a_binding_or_default()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Spec = new ExecutableSpec(),
            Inputs = [new InputPort(Input, isStatic: true)],
        };

        Assert.Equal(
            ["静态输入端口 输入 没有绑定来源或默认值。"],
            Errors(node.CheckStaticCoverage));
    }

    [Fact]
    public void Static_input_accepts_a_default()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Spec = new ExecutableSpec(),
            Inputs = [new InputPort(Input, isStatic: true, defaultValue: "固定值")],
        };

        Assert.Empty(Errors(node.CheckStaticCoverage));
    }

    private static List<string> Errors(Action<Action<string>> check)
    {
        List<string> errors = [];
        check(errors.Add);

        return errors;
    }
}

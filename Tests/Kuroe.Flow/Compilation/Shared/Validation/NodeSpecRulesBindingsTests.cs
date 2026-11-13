using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Flow.Tasks.Shared;
using Xunit;

namespace Kuroe.Flow.Compilation.Shared.Tests.Validation;

/// <summary>节点输入绑定规则：绑定目标是已声明的静态输入端口，静态输入有绑定或默认值。</summary>
public sealed class NodeSpecRulesBindingsTests
{
    private static readonly PortName Input = new("输入");
    private static readonly PortName Output = new("输出");

    [Fact]
    public void Executable_binding_target_must_be_a_static_input()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Spec = new ExecutableSpec(),
            Inputs = [new InputPort(Input)],
            Bindings = new Dictionary<PortName, PortRef>
            {
                [Input] = PortRef.Named(new NodeLabel("来源"), Output),
            },
        };

        Assert.Equal(
            ["绑定目标 输入 不是静态输入端口，动态输入一律经 From 绑定。"],
            Errors(fail => node.CheckBindings(fail)));
    }

    [Fact]
    public void Static_input_requires_a_binding_or_default()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Spec = new ExecutableSpec(),
            Inputs = [new InputPort(Input, mode: PortMode.Constant)],
        };

        Assert.Equal(
            ["静态输入端口 输入 没有绑定来源或默认值。"],
            Errors(fail => node.CheckBindingCoverage(node.EffectiveInputs, container: false, fail)));
    }

    [Fact]
    public void Static_input_accepts_a_default()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Spec = new ExecutableSpec(),
            Inputs = [new InputPort(Input, mode: PortMode.Constant, defaultValue: "固定值")],
        };

        Assert.Empty(Errors(fail => node.CheckBindingCoverage(node.EffectiveInputs, container: false, fail)));
    }

    [Fact]
    public void Container_static_input_requires_binding()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("组")) with
        {
            Nodes = [NodeSpec.Define(new NodeName("成员"))],
            Inputs = [new InputPort(Input, mode: PortMode.Constant)],
        };

        Assert.Equal(
            ["静态输入端口 输入 没有绑定来源。"],
            Errors(fail => node.CheckBindingCoverage(node.EffectiveInputs, container: true, fail)));
    }

    private static List<string> Errors(Action<ValidationMessageHandler> check)
    {
        List<string> errors = [];
        check(errors.Add);

        return errors;
    }
}

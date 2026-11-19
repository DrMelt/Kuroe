using Kuroe.Flow.Graph.Shared.Model;
using Kuroe.Flow.Graph.Shared.StaticGraph.Nodes;
using Xunit;

namespace Kuroe.Flow.Graph.Shared.Tests.StaticGraph.Nodes;

/// <summary>节点声明：生效类型只对展开后的节点树可取，执行方式有缺省值。</summary>
public sealed class NodeSpecTests
{
    [Fact(DisplayName = "生效种类返回声明的种类")]
    public void Effective_kind_returns_the_declared_kind()
    {
        NodeSpec node = new() { Label = new NodeLabel("实施"), Type = Typed("Executable") };

        Assert.Equal(new NodeKind("Executable"), node.EffectiveKind);
    }

    [Fact(DisplayName = "引用节点取生效种类失败并带节点标签")]
    public void Effective_kind_on_a_reference_node_fails_with_the_node_label()
    {
        NodeSpec reference = new() { Label = new NodeLabel("复用"), Use = new NodeName("实施") };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => reference.EffectiveKind);

        Assert.Contains("复用", error.Message);
    }

    [Fact(DisplayName = "节点默认值为空")]
    public void Node_defaults_stay_empty()
    {
        NodeSpec node = new() { Label = new NodeLabel("实施"), Type = Typed("Executable") };

        Assert.Empty(node.Inputs);
        Assert.Empty(node.From);
        Assert.Empty(node.Outputs);
        Assert.Null(node.Use);
        Assert.Null(node.Spec);
        Assert.Null(node.Nodes);
    }

    [Fact(DisplayName = "种类来自类型实例")]
    public void Kind_comes_from_the_type_instance() =>
        Assert.Equal(new NodeKind("Executable"), new NodeSpec { Label = new NodeLabel("实施"), Type = Typed("Executable") }.Kind);

    [Fact(DisplayName = "执行节点默认没有执行上限")]
    public void Executable_defaults_without_limit()
    {
        ExecutableSpec execution = new();

        Assert.Null(execution.MaxExecutions);
    }

    private static StubNodeType Typed(string kind) => new(new NodeKind(kind));
}

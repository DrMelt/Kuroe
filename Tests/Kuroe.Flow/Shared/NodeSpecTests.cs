using Kuroe.Flow.Shared.StaticGraph;
using Xunit;

namespace Kuroe.Flow.Shared.Tests;

/// <summary>节点声明：生效类型只对展开后的节点树可取，执行方式有缺省值。</summary>
public sealed class NodeSpecTests
{
    [Fact]
    public void Effective_kind_returns_the_declared_kind()
    {
        NodeSpec node = new() { Name = new NodeName("实施"), Type = Typed("Executable") };

        Assert.Equal(new NodeKind("Executable"), node.EffectiveKind);
    }

    [Fact]
    public void Effective_kind_on_a_reference_node_fails_with_the_node_name()
    {
        NodeSpec reference = new() { Name = new NodeName("复用"), Use = new NodeName("实施") };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => reference.EffectiveKind);

        Assert.Contains("复用", error.Message);
    }

    [Fact]
    public void Node_defaults_stay_empty()
    {
        NodeSpec node = new() { Name = new NodeName("实施"), Type = Typed("Executable") };

        Assert.Empty(node.Inputs);
        Assert.Empty(node.From);
        Assert.Empty(node.Outputs);
        Assert.Null(node.Use);
        Assert.Null(node.Execution);
        Assert.Null(node.Nodes);
    }

    [Fact]
    public void Kind_comes_from_the_type_instance() =>
        Assert.Equal(new NodeKind("Executable"), new NodeSpec { Name = new NodeName("实施"), Type = Typed("Executable") }.Kind);

    [Fact]
    public void Executable_defaults_to_single_without_branch_or_validation()
    {
        ExecutableSpec execution = new();

        Assert.Equal(NodeMode.Single, execution.Mode);
        Assert.Null(execution.Branch);
        Assert.Null(execution.Validate);
        Assert.Null(execution.MaxRuns);
    }

    private static StubNodeType Typed(string kind) => new(new NodeKind(kind));
}

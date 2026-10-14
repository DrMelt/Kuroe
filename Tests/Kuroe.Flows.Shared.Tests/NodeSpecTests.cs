using Kuroe.Flows.Shared.StaticGraph;
using Xunit;

namespace Kuroe.Flows.Shared.Tests;

/// <summary>节点声明：生效类型只对展开后的节点树可取，执行方式与门控有缺省值。</summary>
public sealed class NodeSpecTests
{
    [Fact]
    public void Effective_kind_returns_the_declared_kind()
    {
        NodeSpec node = new() { Name = new NodeName("实施"), Kind = NodeKind.Executable };

        Assert.Equal(NodeKind.Executable, node.EffectiveKind);
    }

    [Fact]
    public void Effective_kind_on_a_reference_node_fails_with_the_node_name()
    {
        NodeSpec reference = new() { Name = new NodeName("复用"), Kind = null, Use = new NodeName("实施") };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => reference.EffectiveKind);

        Assert.Contains("复用", error.Message);
    }

    [Fact]
    public void Node_defaults_stay_auto_and_empty()
    {
        NodeSpec node = new() { Name = new NodeName("实施"), Kind = NodeKind.Executable };

        Assert.Equal(NodeGate.Auto, node.Gate);
        Assert.Empty(node.Inputs);
        Assert.Empty(node.From);
        Assert.Empty(node.Outputs);
        Assert.Empty(node.SystemPrompt);
        Assert.Null(node.Execution);
        Assert.Null(node.Nodes);
    }

    [Fact]
    public void Executable_defaults_to_single_without_abilities()
    {
        ExecutableSpec execution = new();

        Assert.Equal(NodeMode.Single, execution.Mode);
        Assert.Empty(execution.Tools);
        Assert.Null(execution.Prompt);
        Assert.Null(execution.Question);
        Assert.Null(execution.Branch);
        Assert.Null(execution.Validate);
        Assert.Null(execution.MaxRuns);
    }
}

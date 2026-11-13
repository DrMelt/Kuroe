using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.StaticGraph.Nodes;
using Kuroe.Flow.Node.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Flow.Shared.Tests.StaticGraph.Nodes;

/// <summary>输出端口声明：允许的输出类型由声明方式决定。</summary>
public sealed class OutputDeclarationTests
{
    [Fact]
    public void Declared_uses_the_declared_type_range()
    {
        NodeTypeShape shape = new() { OutputTypes = [PortTypes.Text] };

        Assert.IsType<OutputDeclaration.Declared>(shape.Outputs);
        Assert.Equal([PortTypes.Text], shape.AllowedOutputTypes);
    }

    [Fact]
    public void Derived_outputs_use_the_declared_type_range()
    {
        NodeTypeShape shape = new()
        {
            Outputs = OutputDeclaration.DerivedFromInputs.Instance,
            OutputTypes = [PortTypes.Text, PortTypes.Signal],
        };

        Assert.Equal([PortTypes.Text, PortTypes.Signal], shape.AllowedOutputTypes);
    }

    [Fact]
    public void Given_outputs_use_the_port_types()
    {
        NodeTypeShape shape = new()
        {
            Outputs = new OutputDeclaration.Given(
            [
                new Port(new PortName("Output"), PortTypes.Text),
                new Port(new PortName("Completed"), PortTypes.Signal),
            ]),
        };

        Assert.Equal([PortTypes.Text, PortTypes.Signal], shape.AllowedOutputTypes);
    }
}
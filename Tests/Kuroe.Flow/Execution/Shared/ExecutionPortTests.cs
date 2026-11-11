using Kuroe.Flow.Execution;
using Xunit;

namespace Kuroe.Flow.Execution.Shared.Tests;

/// <summary>执行端口的构造、请求装配、契约与产出默认行为。</summary>
public sealed class ExecutionPortTests
{
    [Fact]
    public void Create_trims_port_name()
    {
        ExecutionPortName name = ExecutionPortName.Create("  input  ").Value;

        Assert.Equal("input", name.Value);
    }

    [Fact]
    public void Create_rejects_blank_or_at_sign()
    {
        Assert.True(ExecutionPortName.Create("   ").IsError);
        Assert.True(ExecutionPortName.Create("input@host").IsError);
    }

    [Fact]
    public void Request_keeps_execution_ports_and_values()
    {
        ExecutionPortName input = new("input");
        ExecutionPortName output = new("output");
        ExecutionRequest request = new(
            new ExecutionOwnerId("task"),
            new ExecutionTargetId("step"),
            "test",
            1,
            [new ExecutionInputPort(input, ExecutionPortKind.Text, ExecutionPortRole.Content, true, null)],
            [new ExecutionInputValue(input, "hello", "start")],
            new Dictionary<ExecutionPortName, string> { [input] = "hello" },
            [new ExecutionOutputPort(output, ExecutionPortKind.Signal, true, true)]);

        Assert.Equal("task", request.Owner.Value);
        Assert.Equal("step", request.Target.Value);
        Assert.Equal("test", request.Kind);
        Assert.Equal(1, request.Attempt);
        Assert.Equal(input, Assert.Single(request.InputPorts).Name);
        Assert.Equal("hello", Assert.Single(request.Inputs).Text);
        Assert.Equal("hello", request.Texts[input]);
        Assert.True(Assert.Single(request.OutputPorts).CarriesTrigger);
    }

    [Fact]
    public void Contract_empty_has_no_outputs()
    {
        Assert.Null(ExecutionContract.Empty.Outputs);
        Assert.Null(ExecutionContract.Empty.CannotCompleteReason);
    }

    [Fact]
    public void Output_value_fires_by_default()
    {
        ExecutionOutputValue value = new(new ExecutionPortName("output"), "done");

        Assert.True(value.Fires);
    }

    [Fact]
    public void Output_can_carry_content_without_ports()
    {
        ExecutionOutput output = ExecutionOutput.FromContent("done");

        Assert.Equal(ExecutionOutputKind.Content, output.Kind);
        Assert.Empty(output.Values);
        Assert.Equal("done", output.Content);
    }
}
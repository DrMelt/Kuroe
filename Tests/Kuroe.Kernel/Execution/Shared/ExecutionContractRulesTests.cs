using Xunit;
using Kuroe.Execution.Shared.Contract;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Ports;
using Kuroe.Execution.Shared.Request;

namespace Kuroe.Execution.Shared.Tests;

/// <summary>执行契约的端口级校验。</summary>
public sealed class ExecutionContractRulesTests
{
    private static readonly ExecutionPortName Required = new("required");

    [Fact]
    public void Validate_accepts_declared_required_output()
    {
        ExecutionRequest request = Request(new ExecutionOutputPort(Required, ExecutionPortKind.Text, true, false));
        ExecutionContract contract = new(
            new Dictionary<ExecutionPortName, ExecutionOutputValue>
            {
                [Required] = new(Required, "done"),
            });

        Assert.False(ExecutionContractRules.Validate(request, contract).IsError);
    }

    [Fact]
    public void Validate_rejects_unknown_output()
    {
        ExecutionRequest request = Request(new ExecutionOutputPort(Required, ExecutionPortKind.Text, true, false));
        ExecutionContract contract = new(
            new Dictionary<ExecutionPortName, ExecutionOutputValue>
            {
                [new ExecutionPortName("unknown")] = new(new ExecutionPortName("unknown"), "done"),
            });

        Assert.True(ExecutionContractRules.Validate(request, contract).IsError);
    }

    [Fact]
    public void Validate_rejects_missing_required_output()
    {
        ExecutionRequest request = Request(new ExecutionOutputPort(Required, ExecutionPortKind.Text, true, false));

        Assert.True(ExecutionContractRules.Validate(request, ExecutionContract.Empty).IsError);
    }

    [Fact]
    public void Validate_accepts_cannot_complete_without_required_output()
    {
        ExecutionRequest request = Request(new ExecutionOutputPort(Required, ExecutionPortKind.Text, true, false));
        ExecutionContract contract = new(CannotCompleteReason: "无法完成");

        Assert.False(ExecutionContractRules.Validate(request, contract).IsError);
    }

    [Fact]
    public void Validate_rejects_blank_cannot_complete_reason()
    {
        ExecutionRequest request = Request(new ExecutionOutputPort(Required, ExecutionPortKind.Text, true, false));
        ExecutionContract contract = new(CannotCompleteReason: "   ");

        Assert.True(ExecutionContractRules.Validate(request, contract).IsError);
    }

    [Fact]
    public void PrepareOutputs_accepts_declared_ports()
    {
        ExecutionRequest request = Request(new ExecutionOutputPort(Required, ExecutionPortKind.Text, true, false));

        var prepared = ExecutionContractRules.PrepareOutputs(request, [new ExecutionOutputValue(Required, "done")]);

        Assert.False(prepared.IsError);
        Assert.Equal("done", prepared.Value[Required].Text);
    }

    [Fact]
    public void PrepareOutputs_rejects_a_silent_bit_on_a_plain_port()
    {
        ExecutionRequest request = Request(new ExecutionOutputPort(Required, ExecutionPortKind.Text, true, false));

        Assert.True(ExecutionContractRules.PrepareOutputs(
            request,
            [new ExecutionOutputValue(Required, "done", false)]).IsError);
    }

    [Fact]
    public void PrepareOutputs_requires_every_required_port()
    {
        ExecutionRequest request = Request(new ExecutionOutputPort(Required, ExecutionPortKind.Text, true, false));

        Assert.True(ExecutionContractRules.PrepareOutputs(
            request,
            [new ExecutionOutputValue(new ExecutionPortName("other"), "done", false)]).IsError);
    }

    [Fact]
    public void PrepareOutputs_rejects_duplicate_ports()
    {
        ExecutionRequest request = Request(new ExecutionOutputPort(Required, ExecutionPortKind.Text, true, false));

        Assert.True(ExecutionContractRules.PrepareOutputs(
            request,
            [
                new ExecutionOutputValue(Required, "done", false),
                new ExecutionOutputValue(Required, "again", false),
            ]).IsError);
    }

    private static ExecutionRequest Request(ExecutionOutputPort output) =>
        new(
            new ExecutionOwnerId("task"),
            new ExecutionStepId("step"),
            [],
            [],
            new Dictionary<ExecutionPortName, string>(),
            [output]);
}

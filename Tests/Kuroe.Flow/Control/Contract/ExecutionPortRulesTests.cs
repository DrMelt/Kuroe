using ErrorOr;
using Kuroe.Execution.Shared;
using Xunit;

namespace Kuroe.Flow.Control.Contract.Tests;

/// <summary>端口交回校验：直接构造执行请求与交回值，断言错误或成功，不创建执行与任务。</summary>
public sealed class ExecutionPortRulesTests
{
    private static readonly ExecutionPortName Conclusion = new("结论");
    private static readonly ExecutionPortName Reason = new("理由");
    private static readonly ExecutionPortName Signal = new("信号");

    [Fact]
    public void Values_must_match_required_ports()
    {
        ErrorOr<Success> result = ExecutionPortRules.Validate(
            Request(Text(Conclusion), Text(Reason)),
            Values((Conclusion, Value(Conclusion, "内容"))));

        Assert.Contains("端口必须与需交回的端口一一对应", Assert.Single(result.ErrorsOrEmptyList).Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_output_is_rejected()
    {
        ErrorOr<Success> result = ExecutionPortRules.Validate(Request(Text(Conclusion)), Values());

        Assert.Contains("端口产出为空", Assert.Single(result.ErrorsOrEmptyList).Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Execution_without_required_ports_is_rejected()
    {
        ErrorOr<Success> result = ExecutionPortRules.Validate(Request(), Values((Conclusion, Value(Conclusion, "内容"))));

        Assert.Contains("该执行没有需要交回的输出端口", Assert.Single(result.ErrorsOrEmptyList).Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_port_requires_content()
    {
        ErrorOr<Success> result = ExecutionPortRules.Validate(
            Request(Text(Conclusion)),
            Values((Conclusion, Value(Conclusion, " "))));

        Assert.Contains("端口 结论 的产出为空", Assert.Single(result.ErrorsOrEmptyList).Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Signal_port_accepts_its_name_alone()
    {
        ErrorOr<Success> result = ExecutionPortRules.Validate(
            Request(SignalPort()),
            Values((Signal, Value(Signal, null))));

        Assert.False(result.IsError);
    }

    [Fact]
    public void Silent_port_rejects_a_false_trigger()
    {
        ErrorOr<Success> result = ExecutionPortRules.Validate(
            Request(Text(Conclusion)),
            Values((Conclusion, Value(Conclusion, "内容", false))));

        Assert.Contains("端口 结论 不承载触发值，不能给出触发位", Assert.Single(result.ErrorsOrEmptyList).Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Trigger_port_accepts_a_false_trigger()
    {
        ErrorOr<Success> result = ExecutionPortRules.Validate(
            Request(new ExecutionOutputPort(Conclusion, ExecutionPortKind.Text, true, true)),
            Values((Conclusion, Value(Conclusion, "内容", false))));

        Assert.False(result.IsError);
    }

    [Fact]
    public void Signal_port_accepts_a_false_trigger()
    {
        ErrorOr<Success> result = ExecutionPortRules.Validate(
            Request(SignalPort()),
            Values((Signal, Value(Signal, null, false))));

        Assert.False(result.IsError);
    }

    private static ExecutionRequest Request(params ExecutionOutputPort[] ports) =>
        new(
            new ExecutionOwnerId("7"),
            new ExecutionTargetId("步骤"),
            "test",
            1,
            [],
            [],
            new Dictionary<ExecutionPortName, string>(),
            ports);

    private static ExecutionOutputPort Text(ExecutionPortName name) =>
        new(name, ExecutionPortKind.Text, true, false);

    private static ExecutionOutputPort SignalPort() =>
        new(Signal, ExecutionPortKind.Signal, true, true);

    private static ExecutionOutputValue Value(ExecutionPortName port, string? text, bool fires = true) =>
        new(port, text, fires);

    private static Dictionary<ExecutionPortName, ExecutionOutputValue> Values(
        params (ExecutionPortName Name, ExecutionOutputValue Value)[] values) =>
        values.ToDictionary(value => value.Name, value => value.Value);
}
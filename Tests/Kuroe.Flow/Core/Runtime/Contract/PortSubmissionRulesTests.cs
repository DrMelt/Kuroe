using ErrorOr;
using Kuroe.Flow.Runtime.Contract;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Flow.Tests.Runtime.Contract;

/// <summary>端口交回校验：直接构造端口声明与交回值，断言错误或成功，不创建执行与任务。</summary>
public sealed class PortSubmissionRulesTests
{
    private static readonly PortName Conclusion = new("结论");
    private static readonly PortName Reason = new("理由");
    private static readonly PortName Signal = new("信号");

    [Fact]
    public void Values_must_match_declared_ports()
    {
        ErrorOr<Success> result = PortSubmissionRules.Validate(
            [Text(Conclusion), Text(Reason)],
            Values((Conclusion, new PortValue("内容"))));

        Assert.Contains("端口必须与需交回的端口一一对应", Assert.Single(result.ErrorsOrEmptyList).Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_output_is_rejected()
    {
        ErrorOr<Success> result = PortSubmissionRules.Validate([Text(Conclusion)], Values());

        Assert.Contains("端口产出为空", Assert.Single(result.ErrorsOrEmptyList).Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Node_without_declared_ports_is_rejected()
    {
        ErrorOr<Success> result = PortSubmissionRules.Validate([], Values((Conclusion, new PortValue("内容"))));

        Assert.Contains("该节点没有需要交回的输出端口", Assert.Single(result.ErrorsOrEmptyList).Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_port_requires_content()
    {
        ErrorOr<Success> result = PortSubmissionRules.Validate(
            [Text(Conclusion)],
            Values((Conclusion, new PortValue(" "))));

        Assert.Contains("端口 结论 的产出为空", Assert.Single(result.ErrorsOrEmptyList).Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Signal_port_accepts_its_name_alone()
    {
        ErrorOr<Success> result = PortSubmissionRules.Validate(
            [SignalPort()],
            Values((Signal, new PortValue(null))));

        Assert.False(result.IsError);
    }

    [Fact]
    public void Silent_port_rejects_a_false_trigger()
    {
        ErrorOr<Success> result = PortSubmissionRules.Validate(
            [Text(Conclusion)],
            Values((Conclusion, new PortValue("内容", false))));

        Assert.Contains("端口 结论 不承载触发值，不能给出触发位", Assert.Single(result.ErrorsOrEmptyList).Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Trigger_port_accepts_a_false_trigger()
    {
        ErrorOr<Success> result = PortSubmissionRules.Validate(
            [new Port(Conclusion, PortTypes.Text, mode: PortMode.Trigger)],
            Values((Conclusion, new PortValue("内容", false))));

        Assert.False(result.IsError);
    }

    [Fact]
    public void Signal_port_accepts_a_false_trigger()
    {
        ErrorOr<Success> result = PortSubmissionRules.Validate(
            [SignalPort()],
            Values((Signal, new PortValue(null, false))));

        Assert.False(result.IsError);
    }

    private static Port Text(PortName name) => new(name, PortTypes.Text);

    private static Port SignalPort() => new(Signal, PortTypes.Signal);

    private static Dictionary<PortName, PortValue> Values(params (PortName Name, PortValue Value)[] values) =>
        values.ToDictionary(value => value.Name, value => value.Value);
}

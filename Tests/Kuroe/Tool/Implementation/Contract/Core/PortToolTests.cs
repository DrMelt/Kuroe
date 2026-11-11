using System.Text.Json;
using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Tool.Contract.Tests;

/// <summary>交回端口产出的载体：说明只写用途，结构 schema 给出端口与形状，回执给出本轮尚缺的端口。</summary>
public sealed class PortToolTests
{
    private static readonly PortName Conclusion = new("结论");
    private static readonly PortName Reason = new("理由");

    [Fact]
    public void Description_states_the_purpose_without_repeating_ports()
    {
        PortTool tool = new(new FakeSubmitter(), new ExecutionId(1), [Port(Conclusion), Port(Reason)]);

        ToolFunction function = Assert.Single(tool.Functions);
        Assert.Equal("提交本执行节点命名输出端口的值。", function.Definition.Description);

        JsonElement properties = Assert.Single(function.Definition.Parameters)
            .Schema.GetValueOrDefault().GetProperty("properties");
        Assert.True(properties.TryGetProperty("结论", out _));
        Assert.True(properties.TryGetProperty("理由", out _));
    }

    [Fact]
    public void Submit_renders_the_recorded_ports_and_the_remaining_ones()
    {
        FakeSubmitter submitter = new()
        {
            Outcome = Status([Reason]),
        };
        PortTool tool = new(submitter, new ExecutionId(1), [Port(Conclusion), Port(Reason)]);

        ErrorOr<string> result = Assert.Single(tool.Functions)
            .Invoke(Given(("valuesJson", """{ "结论": "完成" }""")));

        Assert.False(result.IsError);
        Assert.Contains("已记录 1 个端口的产出", result.Value, StringComparison.Ordinal);
        Assert.Contains("本轮仍需交回：端口「理由」", result.Value, StringComparison.Ordinal);
    }

    /// <summary>顶层键只认声明端口名，trigger 写在顶层按未知端口拒绝。</summary>
    [Fact]
    public void Submit_rejects_a_top_level_trigger_key_as_an_unknown_port()
    {
        PortTool tool = new(new FakeSubmitter(), new ExecutionId(1), [Port(Conclusion)]);

        ErrorOr<string> result = Assert.Single(tool.Functions)
            .Invoke(Given(("valuesJson", """{ "结论": "完成", "trigger": false }""")));

        Assert.True(result.IsError);
        Assert.Contains("不认识端口 trigger", result.FirstError.Description, StringComparison.Ordinal);
        Assert.Contains("应提交 结论", result.FirstError.Description, StringComparison.Ordinal);
    }

    /// <summary>端口名按原文匹配，大小写不同按未知端口拒绝。</summary>
    [Fact]
    public void Submit_rejects_a_case_different_port_name()
    {
        PortTool tool = new(new FakeSubmitter(), new ExecutionId(1),
            [new Port(new PortName("Trigger"), PortTypes.Text)]);

        ErrorOr<string> result = Assert.Single(tool.Functions)
            .Invoke(Given(("valuesJson", """{ "trigger": "完成" }""")));

        Assert.True(result.IsError);
        Assert.Contains("不认识端口 trigger", result.FirstError.Description, StringComparison.Ordinal);
        Assert.Contains("应提交 Trigger", result.FirstError.Description, StringComparison.Ordinal);
    }

    /// <summary>声明端口名恰为 trigger 时按普通端口接受。</summary>
    [Fact]
    public void Submit_accepts_a_port_named_trigger()
    {
        PortTool tool = new(new FakeSubmitter(), new ExecutionId(1),
            [new Port(new PortName("trigger"), PortTypes.Text)]);

        ErrorOr<string> result = Assert.Single(tool.Functions)
            .Invoke(Given(("valuesJson", """{ "trigger": "完成" }""")));

        Assert.False(result.IsError);
    }

    /// <summary>对象字段按原文匹配，大小写不同按未知键拒绝。</summary>
    [Fact]
    public void Submit_rejects_a_case_different_object_field()
    {
        PortTool tool = new(new FakeSubmitter(), new ExecutionId(1),
            [new Port(Conclusion, PortTypes.Text, mode: PortMode.Trigger)]);

        ErrorOr<string> result = Assert.Single(tool.Functions)
            .Invoke(Given(("valuesJson", """{ "结论": { "Text": "完成", "trigger": true } }""")));

        Assert.True(result.IsError);
        Assert.Contains("不认识键 Text", result.FirstError.Description, StringComparison.Ordinal);
    }

    /// <summary>端口名首尾空白按端口名规则去掉后匹配。</summary>
    [Fact]
    public void Submit_normalizes_surrounding_whitespace_in_port_names()
    {
        FakeSubmitter submitter = new();
        PortTool tool = new(submitter, new ExecutionId(1), [Port(Conclusion)]);

        ErrorOr<string> result = Assert.Single(tool.Functions)
            .Invoke(Given(("valuesJson", """{ " 结论 ": "完成" }""")));

        Assert.False(result.IsError);
        Assert.True(submitter.Submitted is { } submitted && submitted.ContainsKey(Conclusion));
    }

    /// <summary>去掉首尾空白后同名的端口键按重复提交拒绝。</summary>
    [Fact]
    public void Submit_rejects_duplicate_port_names_after_normalization()
    {
        PortTool tool = new(new FakeSubmitter(), new ExecutionId(1), [Port(Conclusion)]);

        ErrorOr<string> result = Assert.Single(tool.Functions)
            .Invoke(Given(("valuesJson", """{ "结论": "完成", " 结论 ": "重复" }""")));

        Assert.True(result.IsError);
        Assert.Contains("重复提交", result.FirstError.Description, StringComparison.Ordinal);
    }

    /// <summary>普通端口不接受 JSON null。</summary>
    [Fact]
    public void Submit_rejects_a_null_plain_port_value()
    {
        PortTool tool = new(new FakeSubmitter(), new ExecutionId(1), [Port(Conclusion)]);

        ErrorOr<string> result = Assert.Single(tool.Functions)
            .Invoke(Given(("valuesJson", """{ "结论": null }""")));

        Assert.True(result.IsError);
        Assert.Contains("普通端口只接受文本", result.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Submit_accepts_strict_trigger_objects()
    {
        PortName signal = new("信号");
        PortTool tool = new(new FakeSubmitter(), new ExecutionId(1),
        [
            new Port(Conclusion, PortTypes.Text, mode: PortMode.Trigger),
            new Port(signal, PortTypes.Signal, mode: PortMode.Trigger),
        ]);

        ErrorOr<string> result = Assert.Single(tool.Functions)
            .Invoke(Given(("valuesJson",
                """{ "结论": { "text": "完成", "trigger": true }, "信号": { "trigger": false } }""")));

        Assert.False(result.IsError);
        Assert.Contains("已记录 2 个端口的产出", result.Value, StringComparison.Ordinal);
        Assert.Contains("其中 1 个端口不触发下游", result.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Submit_rejects_a_text_trigger_port_written_as_text()
    {
        PortTool tool = new(new FakeSubmitter(), new ExecutionId(1),
            [new Port(Conclusion, PortTypes.Text, mode: PortMode.Trigger)]);

        ErrorOr<string> result = Assert.Single(tool.Functions)
            .Invoke(Given(("valuesJson", """{ "结论": "完成" }""")));

        Assert.True(result.IsError);
        Assert.Contains("取值必须是对象", result.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Submit_rejects_a_text_trigger_object_without_the_trigger_bit()
    {
        PortTool tool = new(new FakeSubmitter(), new ExecutionId(1),
            [new Port(Conclusion, PortTypes.Text, mode: PortMode.Trigger)]);

        ErrorOr<string> result = Assert.Single(tool.Functions)
            .Invoke(Given(("valuesJson", """{ "结论": { "text": "完成" } }""")));

        Assert.True(result.IsError);
        Assert.Contains("对象必须写 trigger", result.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Submit_rejects_a_signal_trigger_port_written_as_a_boolean()
    {
        PortName signal = new("信号");
        PortTool tool = new(new FakeSubmitter(), new ExecutionId(1),
            [new Port(signal, PortTypes.Signal, mode: PortMode.Trigger)]);

        ErrorOr<string> result = Assert.Single(tool.Functions)
            .Invoke(Given(("valuesJson", """{ "信号": false }""")));

        Assert.True(result.IsError);
        Assert.Contains("取值必须是对象", result.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Submit_rejects_a_signal_trigger_object_without_the_trigger_bit()
    {
        PortName signal = new("信号");
        PortTool tool = new(new FakeSubmitter(), new ExecutionId(1),
            [new Port(signal, PortTypes.Signal, mode: PortMode.Trigger)]);

        ErrorOr<string> result = Assert.Single(tool.Functions)
            .Invoke(Given(("valuesJson", """{ "信号": {} }""")));

        Assert.True(result.IsError);
        Assert.Contains("对象必须写 trigger", result.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Definition_declares_the_port_structure_in_the_parameter_schema()
    {
        PortName signal = new("信号");
        PortName completed = new("完成");
        PortTool tool = new(new FakeSubmitter(), new ExecutionId(1),
        [
            new Port(Conclusion, PortTypes.Text, "是否通过", mode: PortMode.Trigger),
            new Port(signal, PortTypes.Signal, mode: PortMode.Trigger),
            Port(Reason),
            new Port(completed, PortTypes.Signal),
        ]);

        ToolParameter parameter = Assert.Single(Assert.Single(tool.Functions).Definition.Parameters);
        Assert.True(parameter.Schema.HasValue);

        JsonElement schema = parameter.Schema.GetValueOrDefault();
        Assert.Equal(
            "端口名到值的对象。每个声明的端口都必须提交，不能提交未声明的端口；触发位写在端口对象内。",
            schema.GetProperty("description").GetString());
        JsonElement properties = schema.GetProperty("properties");
        JsonElement text = properties.GetProperty("结论");
        JsonElement pulse = properties.GetProperty("信号");
        JsonElement plain = properties.GetProperty("完成");
        Assert.Equal("object", text.GetProperty("type").GetString());
        Assert.Equal("是否通过", text.GetProperty("description").GetString());
        Assert.Equal(["text", "trigger"], Required(text));
        Assert.Equal("string", text.GetProperty("properties").GetProperty("text").GetProperty("type").GetString());
        Assert.Equal("交回的文本", text.GetProperty("properties").GetProperty("text").GetProperty("description").GetString());
        Assert.Equal("boolean", text.GetProperty("properties").GetProperty("trigger").GetProperty("type").GetString());
        Assert.Equal("object", pulse.GetProperty("type").GetString());
        Assert.Equal(["trigger"], Required(pulse));
        Assert.False(pulse.GetProperty("properties").TryGetProperty("text", out _));
        Assert.Equal("string", properties.GetProperty("理由").GetProperty("type").GetString());
        Assert.Equal("string", plain.GetProperty("type").GetString());
        Assert.Equal("交回空文本或端口名，内容不进入产出", plain.GetProperty("description").GetString());
        Assert.Equal(["结论", "信号", "理由", "完成"], Required(schema));
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public void Definition_omits_the_parameter_schema_without_ports()
    {
        PortTool withoutPorts = new(new FakeSubmitter(), new ExecutionId(1), null);
        PortTool withEmptyList = new(new FakeSubmitter(), new ExecutionId(1), []);

        Assert.False(Assert.Single(Assert.Single(withoutPorts.Functions).Definition.Parameters).Schema.HasValue);
        Assert.False(Assert.Single(Assert.Single(withEmptyList.Functions).Definition.Parameters).Schema.HasValue);
    }

    [Fact]
    public void Submit_rejects_a_text_trigger_object_without_text()
    {
        PortTool tool = new(new FakeSubmitter(), new ExecutionId(1),
            [new Port(Conclusion, PortTypes.Text, mode: PortMode.Trigger)]);

        ErrorOr<string> result = Assert.Single(tool.Functions)
            .Invoke(Given(("valuesJson", """{ "结论": { "trigger": true } }""")));

        Assert.True(result.IsError);
        Assert.Contains("对象必须写 text", result.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Submit_rejects_a_non_text_text_value()
    {
        PortTool tool = new(new FakeSubmitter(), new ExecutionId(1),
            [new Port(Conclusion, PortTypes.Text, mode: PortMode.Trigger)]);

        ErrorOr<string> result = Assert.Single(tool.Functions)
            .Invoke(Given(("valuesJson", """{ "结论": { "text": 1, "trigger": true } }""")));

        Assert.True(result.IsError);
        Assert.Contains("text 必须是文本", result.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Submit_rejects_an_unknown_key_in_a_trigger_object()
    {
        PortTool tool = new(new FakeSubmitter(), new ExecutionId(1),
            [new Port(Conclusion, PortTypes.Text, mode: PortMode.Trigger)]);

        ErrorOr<string> result = Assert.Single(tool.Functions)
            .Invoke(Given(("valuesJson", """{ "结论": { "text": "完成", "trigger": true, "其他": 1 } }""")));

        Assert.True(result.IsError);
        Assert.Contains("不认识键 其他", result.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Submit_rejects_a_non_boolean_trigger_bit()
    {
        PortName signal = new("信号");
        PortTool tool = new(new FakeSubmitter(), new ExecutionId(1),
            [new Port(signal, PortTypes.Signal, mode: PortMode.Trigger)]);

        ErrorOr<string> result = Assert.Single(tool.Functions)
            .Invoke(Given(("valuesJson", """{ "信号": { "trigger": "是" } }""")));

        Assert.True(result.IsError);
        Assert.Contains("trigger 必须是 true 或 false", result.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Submit_rejects_an_object_on_a_plain_text_port()
    {
        PortTool tool = new(new FakeSubmitter(), new ExecutionId(1), [Port(Conclusion)]);

        ErrorOr<string> result = Assert.Single(tool.Functions)
            .Invoke(Given(("valuesJson", """{ "结论": { "text": "完成" } }""")));

        Assert.True(result.IsError);
        Assert.Contains("普通端口只接受文本", result.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Submit_reads_a_structured_argument_from_the_model()
    {
        PortTool tool = new(new FakeSubmitter(), new ExecutionId(1),
            [new Port(Conclusion, PortTypes.Text, mode: PortMode.Trigger)]);

        using JsonDocument document = JsonDocument.Parse("""{ "结论": { "text": "完成", "trigger": true } }""");
        ToolArguments arguments = new(new Dictionary<string, object?>
        {
            ["valuesJson"] = document.RootElement.Clone(),
        });

        ErrorOr<string> result = Assert.Single(tool.Functions).Invoke(arguments);

        Assert.False(result.IsError);
        Assert.Contains("已记录 1 个端口的产出", result.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Submit_reads_a_dictionary_argument_after_restore()
    {
        PortTool tool = new(new FakeSubmitter(), new ExecutionId(1),
            [new Port(Conclusion, PortTypes.Text, mode: PortMode.Trigger)]);

        ToolArguments arguments = new(new Dictionary<string, object?>
        {
            ["valuesJson"] = new Dictionary<string, object?>
            {
                ["结论"] = new Dictionary<string, object?>
                {
                    ["text"] = "完成",
                    ["trigger"] = true,
                },
            },
        });

        ErrorOr<string> result = Assert.Single(tool.Functions).Invoke(arguments);

        Assert.False(result.IsError);
        Assert.Contains("已记录 1 个端口的产出", result.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Submit_error_is_returned_to_the_model()
    {
        FakeSubmitter submitter = new()
        {
            Failure = Error.Validation("Port.Rejected", "端口值不合法。"),
        };
        PortTool tool = new(submitter, new ExecutionId(1), [Port(Conclusion)]);

        ErrorOr<string> result = Assert.Single(tool.Functions)
            .Invoke(Given(("valuesJson", """{ "结论": "完成" }""")));

        Assert.True(result.IsError);
        Assert.Equal("端口值不合法。", result.FirstError.Description);
    }

    private static IEnumerable<string?> Required(JsonElement schema) =>
        schema.GetProperty("required").EnumerateArray().Select(item => item.GetString());

    private static Port Port(PortName name) => new(name, PortTypes.Text);

    private static ExecutionStatus Status(IReadOnlyList<PortName> outstanding) =>
        new(NodePath.Of("节点"), 1, 3, NodeState.Running, outstanding, null);

    private static ToolArguments Given(params (string Name, object? Value)[] values) =>
        new(values.ToDictionary(value => value.Name, value => value.Value));

    private sealed class FakeSubmitter : IPortSubmitter
    {
        public ExecutionStatus Outcome { get; init; } = new(NodePath.Of("节点"), 1, 3, NodeState.Running, [], null);

        public Error? Failure { get; init; }

        public IReadOnlyDictionary<PortName, PortValue>? Submitted { get; private set; }

        public ErrorOr<ExecutionStatus> SubmitValues(
            ExecutionId id,
            IReadOnlyDictionary<PortName, PortValue> values)
        {
            Submitted = values;

            return Failure is { } error ? error : Outcome;
        }
    }
}

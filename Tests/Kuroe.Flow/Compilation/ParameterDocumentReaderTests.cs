using Kuroe.Flow.Compilation.Description;
using Kuroe.Flow.Compilation.Shared;
using Kuroe.Flow.Compilation.Shared.NodeTypes;
using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Xunit;

namespace Kuroe.Flow.Compilation.Tests;

/// <summary>参数字段的描述读取：按字段表转换取值、未知字段与形状不符的报出。</summary>
public sealed class ParameterDocumentReaderTests
{
    [Fact]
    public void Read_converts_values_by_the_declared_shape()
    {
        List<string> errors = [];

        ParameterNodeType type = Assert.IsType<ParameterNodeType>(ReadParameters(
            [
                ("Text", new FlowValue.Text("展开")),
                ("Texts", new FlowValue.Array([new FlowValue.Text("甲"), new FlowValue.Text("乙")])),
                ("Number", new FlowValue.Number("5")),
            ],
            errors));

        Assert.Empty(errors);
        Assert.Equal("展开", type.Parameters!.Text);
        Assert.Equal(["甲", "乙"], type.Parameters.Texts);
        Assert.Equal(5, type.Parameters.Number);
    }

    [Fact]
    public void Read_takes_safe_defaults_for_missing_fields()
    {
        List<string> errors = [];

        ParameterNodeType type = Assert.IsType<ParameterNodeType>(ReadParameters([], errors));

        Assert.Empty(errors);
        Assert.Null(type.Parameters!.Text);
        Assert.Empty(type.Parameters.Texts);
        Assert.Null(type.Parameters.Number);
    }

    [Fact]
    public void Read_reports_unknown_fields() =>
        Assert.Equal(
            ["流程 测试 的节点 干活：字段 Extra 不属于 Test 节点。"],
            Errors([("Text", new FlowValue.Text("展开")), ("Extra", new FlowValue.Text("多余"))]));

    [Fact]
    public void Read_reports_values_of_the_wrong_shape() =>
        Assert.Equal(
            [
                "流程 测试 的节点 干活：字段 Text 必须是文本。",
                "流程 测试 的节点 干活：字段 Texts 必须是文本数组。",
                "流程 测试 的节点 干活：字段 Number 必须是整数。",
            ],
            Errors(
                [
                    ("Text", new FlowValue.Number("5")),
                    ("Texts", new FlowValue.Text("甲")),
                    ("Number", new FlowValue.Text("五")),
                ]));

    [Fact]
    public void Read_reports_numbers_that_are_not_integers() =>
        Assert.Equal(
            ["流程 测试 的节点 干活：字段 Number 必须是整数。"],
            Errors([("Number", new FlowValue.Number("1.5"))]));

    [Fact]
    public void Read_reports_array_items_of_the_wrong_shape() =>
        Assert.Equal(
            ["流程 测试 的节点 干活：字段 Texts 的元素必须是文本。"],
            Errors([("Texts", new FlowValue.Array([new FlowValue.Number("1"), new FlowValue.Text("甲")]))]));

    private static readonly TestNodeTypeDescription Description = new();

    private static INodeType? ReadParameters((string Name, FlowValue Value)[] entries, List<string> errors) =>
        ParameterDocumentReader.Read(
            Description,
            entries.ToDictionary(entry => entry.Name, entry => entry.Value),
            NodeScope.Flow("测试"),
            new NodeLabel("干活"),
            error => errors.Add(error.Description));

    private static List<string> Errors((string Name, FlowValue Value)[] entries)
    {
        List<string> errors = [];
        ReadParameters(entries, errors);

        return errors;
    }

    private sealed class TestNodeTypeDescription : INodeTypeDescription
    {
        public NodeKind Kind => TestNodeTypeFactory.Kind;

        public NodeTypeShape Shape { get; } = new();

        public IReadOnlyList<ParameterField> Fields { get; } =
        [
            new("Text", ParameterValueKind.Text),
            new("Texts", ParameterValueKind.Texts),
            new("Number", ParameterValueKind.Integer),
        ];

        public INodeType Read(IParameterReader values, Action<string> fail) =>
            TestNodeTypeFactory.Create(new TestParameters(values.Text("Text"), values.Texts("Texts"), values.Integer("Number")));
    }

    private sealed class ParameterNodeType(TestParameters? parameters) : INodeType
    {
        public NodeKind Kind => TestNodeTypeFactory.Kind;

        public NodeTypeShape Shape { get; } = new();

        public TestParameters? Parameters { get; } = parameters;
    }

    private static class TestNodeTypeFactory
    {
        public static NodeKind Kind { get; } = new("Test");

        public static ParameterNodeType Create(TestParameters parameters) => new(parameters);
    }

    private sealed record TestParameters(string? Text, IReadOnlyList<string> Texts, int? Number);
}
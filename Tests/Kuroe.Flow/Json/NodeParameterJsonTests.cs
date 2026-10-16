using System.Text.Json;
using ErrorOr;
using Kuroe.Flow.Json.Files;
using Kuroe.Flow.Json.Parameters;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph;
using Xunit;

namespace Kuroe.Flow.Json.Tests;

/// <summary>参数段的 JSON 读写：按字段表转换取值、未知字段与形状不符的报出，以及写回的 JSON 形状。</summary>
public sealed class NodeParameterJsonTests
{
    [Fact]
    public void Read_converts_values_by_the_declared_shape()
    {
        List<string> errors = [];

        TestNodeType type = Assert.IsType<TestNodeType>(ReadParameters([("Text", "\"展开\""), ("Texts", "[\"甲\", \"乙\"]")], errors));

        Assert.Empty(errors);
        Assert.Equal("展开", type.Parameters!.Text);
        Assert.Equal(["甲", "乙"], type.Parameters.Texts);
    }

    [Fact]
    public void Read_takes_safe_defaults_for_missing_fields()
    {
        List<string> errors = [];

        TestNodeType type = Assert.IsType<TestNodeType>(ReadParameters([], errors));

        Assert.Empty(errors);
        Assert.Null(type.Parameters!.Text);
        Assert.Empty(type.Parameters.Texts);
    }

    [Fact]
    public void Read_reports_unknown_fields() =>
        Assert.Equal(
            ["流程 测试 的节点 干活：字段 Extra 不属于 Test 节点。"],
            Errors([("Text", "\"展开\""), ("Extra", "\"多余\"")]));

    [Fact]
    public void Read_reports_values_of_the_wrong_shape() =>
        Assert.Equal(
            ["流程 测试 的节点 干活：字段 Text 必须是文本。", "流程 测试 的节点 干活：字段 Texts 必须是文本数组。"],
            Errors([("Text", "5"), ("Texts", "\"甲\"")]));

    [Fact]
    public void Read_reports_array_items_of_the_wrong_shape() =>
        Assert.Equal(
            ["流程 测试 的节点 干活：字段 Texts 的元素必须是文本。"],
            Errors([("Texts", "[1, \"甲\"]")]));

    [Fact]
    public void Write_serializes_each_field_by_its_kind()
    {
        Dictionary<string, JsonElement> target = [];

        NodeParameterJson.Write(File, Typed("展开", ["甲", "乙"]), target);

        Assert.Equal(JsonValueKind.String, target["Text"].ValueKind);
        Assert.Equal("展开", target["Text"].GetString());
        Assert.Equal(JsonValueKind.Array, target["Texts"].ValueKind);
        Assert.Equal(["甲", "乙"], target["Texts"].EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public void Write_omits_fields_the_type_does_not_write()
    {
        Dictionary<string, JsonElement> target = [];

        NodeParameterJson.Write(File, Typed(null, []), target);

        Assert.Empty(target);
    }

    private static readonly TestTypeFile File = new();

    private static INodeType ReadParameters((string Name, string Json)[] entries, List<string> errors) =>
        NodeParameterJson.Read(
            File,
            NodeScope.Flow("测试"),
            new NodeName("干活"),
            Values(entries),
            error => errors.Add(error.Description));

    private static List<string> Errors((string Name, string Json)[] entries)
    {
        List<string> errors = [];
        ReadParameters(entries, errors);

        return errors;
    }

    private static Dictionary<string, JsonElement> Values((string Name, string Json)[] entries)
    {
        Dictionary<string, JsonElement> values = [];
        foreach ((string name, string json) in entries)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            values[name] = document.RootElement.Clone();
        }

        return values;
    }

    private static TestNodeType Typed(string? text, IReadOnlyList<string> texts) =>
        TestNodeTypes.Create(new TestParameters(text, texts));

    /// <summary>测试用的类型文件形状：一个文本字段与一个文本数组字段。</summary>
    private sealed class TestTypeFile : INodeTypeFile
    {
        /// <summary>类型名。</summary>
        public NodeKind Kind => TestNodeTypes.Name;

        /// <summary>静态形状，取默认。</summary>
        public NodeTypeShape Shape { get; } = new();

        /// <summary>字段表。</summary>
        public IReadOnlyList<ParameterField> Fields { get; } =
        [
            new("Text", ParameterValueKind.Text),
            new("Texts", ParameterValueKind.Texts),
        ];

        /// <summary>按字段取用本轮取值并建出类型实例。</summary>
        public INodeType Read(IParameterReader values, Action<string> fail) =>
            TestNodeTypes.Create(new TestParameters(values.Text("Text"), values.Texts("Texts")));

        /// <summary>只写有值的字段。</summary>
        public ParameterValues Write(INodeType? type)
        {
            ParameterValues values = new();
            if (type is not TestNodeType { Parameters: { } parameters })
            {
                return values;
            }

            if (parameters.Text is { Length: > 0 } text)
            {
                values.Set("Text", text);
            }

            if (parameters.Texts.Count > 0)
            {
                values.Set("Texts", parameters.Texts);
            }

            return values;
        }
    }

    /// <summary>测试用的节点类型：持有测试声明。</summary>
    private sealed class TestNodeType(TestParameters? parameters) : INodeType
    {
        /// <summary>类型名。</summary>
        public NodeKind Kind => TestNodeTypes.Name;

        /// <summary>静态形状，取默认。</summary>
        public NodeTypeShape Shape { get; } = new();

        /// <summary>本节点的声明。</summary>
        public TestParameters? Parameters { get; } = parameters;

        /// <summary>类型规则，替身没有约束。</summary>
        public void Validate(NodeSpec node, NodeScope scope, Action<Error> fail)
        {
        }
    }

    /// <summary>测试用的类型入口。</summary>
    private static class TestNodeTypes
    {
        /// <summary>类型名。</summary>
        public static NodeKind Name { get; } = new("Test");

        /// <summary>建出类型实例。</summary>
        public static TestNodeType Create(TestParameters parameters) => new(parameters);
    }

    /// <summary>测试用的类型声明。</summary>
    private sealed record TestParameters(string? Text, IReadOnlyList<string> Texts);
}
using System.Text.Json;
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

        Parameters parameters = Assert.IsType<Parameters>(ReadParameters([("Text", "\"展开\""), ("Texts", "[\"甲\", \"乙\"]")], errors));

        Assert.Empty(errors);
        Assert.Equal("展开", parameters.Text);
        Assert.Equal(["甲", "乙"], parameters.Texts);
    }

    [Fact]
    public void Read_takes_safe_defaults_for_missing_fields()
    {
        List<string> errors = [];

        Parameters parameters = Assert.IsType<Parameters>(ReadParameters([], errors));

        Assert.Empty(errors);
        Assert.Null(parameters.Text);
        Assert.Empty(parameters.Texts);
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

        NodeParameterJson.Write(Shape, new Parameters("展开", ["甲", "乙"]), target);

        Assert.Equal(JsonValueKind.String, target["Text"].ValueKind);
        Assert.Equal("展开", target["Text"].GetString());
        Assert.Equal(JsonValueKind.Array, target["Texts"].ValueKind);
        Assert.Equal(["甲", "乙"], target["Texts"].EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public void Write_omits_fields_the_type_does_not_write()
    {
        Dictionary<string, JsonElement> target = [];

        NodeParameterJson.Write(Shape, new Parameters(null, []), target);

        Assert.Empty(target);
    }

    private static readonly TestParameterShape Shape = new();

    private static INodeParameters? ReadParameters((string Name, string Json)[] entries, List<string> errors) =>
        NodeParameterJson.Read(
            Shape,
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

    /// <summary>测试用的参数段形状：一个文本字段与一个文本数组字段。</summary>
    private sealed class TestParameterShape : INodeParameterFile
    {
        /// <summary>类型名。</summary>
        public NodeKind Kind { get; } = new("Test");

        /// <summary>字段表。</summary>
        public IReadOnlyList<ParameterField> Fields { get; } =
        [
            new("Text", ParameterValueKind.Text),
            new("Texts", ParameterValueKind.Texts),
        ];

        /// <summary>按字段取用本轮取值。</summary>
        public INodeParameters? Read(IParameterReader values, Action<string> fail) =>
            new Parameters(values.Text("Text"), values.Texts("Texts"));

        /// <summary>只写有值的字段。</summary>
        public ParameterValues Write(INodeParameters? parameters)
        {
            ParameterValues values = new();
            if (parameters is not Parameters shape)
            {
                return values;
            }

            if (shape.Text is { Length: > 0 } text)
            {
                values.Set("Text", text);
            }

            if (shape.Texts.Count > 0)
            {
                values.Set("Texts", shape.Texts);
            }

            return values;
        }
    }

    /// <summary>测试用的参数对象。</summary>
    private sealed record Parameters(string? Text, IReadOnlyList<string> Texts) : INodeParameters;
}
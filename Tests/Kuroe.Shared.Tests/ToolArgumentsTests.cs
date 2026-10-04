using System.Text.Json;
using Kuroe.Shared.Executions.Tools;
using Xunit;

namespace Kuroe.Shared.Tests;

/// <summary>工具实参读取：标量按文本给出，真假值单独认取。</summary>
public sealed class ToolArgumentsTests
{
    [Fact]
    public void Text_reads_scalars_and_other_values_as_text()
    {
        ToolArguments arguments = Arguments("""{"city": "北京", "passed": true, "count": 3}""");

        Assert.Equal("北京", arguments.Text(new ToolName("city")));
        Assert.Equal("True", arguments.Text(new ToolName("passed")));
        Assert.Equal("3", arguments.Text(new ToolName("count")));
        Assert.Null(arguments.Text(new ToolName("missing")));
    }

    [Fact]
    public void Flag_reads_bool_and_parses_text_forms()
    {
        ToolArguments arguments = Arguments("""{"yes": true, "no": "false", "city": "北京", "none": null}""");

        Assert.True(arguments.Flag(new ToolName("yes")));
        Assert.False(arguments.Flag(new ToolName("no")));
        Assert.Null(arguments.Flag(new ToolName("city")));
        Assert.Null(arguments.Flag(new ToolName("none")));
        Assert.Null(arguments.Flag(new ToolName("missing")));
    }

    [Fact]
    public void Clr_values_are_read_like_json_ones()
    {
        ToolArguments arguments = new(new Dictionary<string, object?>
        {
            ["city"] = "北京",
            ["passed"] = true,
            ["count"] = 3,
        });

        Assert.Equal("北京", arguments.Text(new ToolName("city")));
        Assert.True(arguments.Flag(new ToolName("passed")));
        Assert.Equal("3", arguments.Text(new ToolName("count")));
    }

    [Fact]
    public void Parameters_default_to_string_and_not_required()
    {
        ToolParameter parameter = new(new ToolName("city"), "城市的中文名称");

        Assert.False(parameter.Flag);
        Assert.False(parameter.Required);
        Assert.False(parameter.List);
    }

    [Fact]
    public void List_reads_json_arrays_as_texts()
    {
        ToolArguments arguments = Arguments("""{"files": ["a", "b", "", null], "single": "x", "none": null}""");

        Assert.Equal(["a", "b", "", ""], [.. arguments.List(new ToolName("files"))!]);
        Assert.Null(arguments.List(new ToolName("single")));
        Assert.Null(arguments.List(new ToolName("none")));
        Assert.Null(arguments.List(new ToolName("missing")));
    }

    private static readonly string[] values = ["a", "b"];

    [Fact]
    public void List_reads_clr_string_arrays()
    {
        ToolArguments arguments = new(new Dictionary<string, object?>
        {
            ["files"] = values,
        });

        Assert.Equal(["a", "b"], [.. arguments.List(new ToolName("files"))!]);
    }

    /// <summary>实参按模型给出的 JSON 值构造，值各自持有，不随文档释放失效。</summary>
    private static ToolArguments Arguments(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        return new ToolArguments(document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value.Clone()));
    }
}
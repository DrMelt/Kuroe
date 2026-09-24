using System.Text.Json;
using Kuroe.Shared.Agent.Tools;
using Xunit;

namespace Kuroe.Shared.Tests;

/// <summary>工具实参读取：标量按文本给出，真假值单独认取。</summary>
public sealed class ToolArgumentsTests
{
    [Fact]
    public void Text_reads_scalars_and_other_values_as_text()
    {
        ToolArguments arguments = Arguments("""{"city": "北京", "passed": true, "count": 3}""");

        Assert.Equal("北京", arguments.Text("city"));
        Assert.Equal("True", arguments.Text("passed"));
        Assert.Equal("3", arguments.Text("count"));
        Assert.Null(arguments.Text("missing"));
    }

    [Fact]
    public void Flag_reads_bool_and_parses_text_forms()
    {
        ToolArguments arguments = Arguments("""{"yes": true, "no": "false", "city": "北京", "none": null}""");

        Assert.True(arguments.Flag("yes"));
        Assert.False(arguments.Flag("no"));
        Assert.Null(arguments.Flag("city"));
        Assert.Null(arguments.Flag("none"));
        Assert.Null(arguments.Flag("missing"));
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

        Assert.Equal("北京", arguments.Text("city"));
        Assert.True(arguments.Flag("passed"));
        Assert.Equal("3", arguments.Text("count"));
    }

    [Fact]
    public void Parameters_default_to_string_and_not_required()
    {
        ToolParameter parameter = new("city", "城市的中文名称");

        Assert.False(parameter.Flag);
        Assert.False(parameter.Required);
    }

    /// <summary>实参按模型给出的 JSON 值构造，值各自持有，不随文档释放失效。</summary>
    private static ToolArguments Arguments(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        return new ToolArguments(document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value.Clone()));
    }
}
using System.Text.Json;
using Xunit;

namespace Kuroe.Ability.Tool.Tests;

/// <summary>工具实参读取：标量按文本给出，真假值单独认取，对象按 JSON 文本给出。</summary>
public sealed class ToolArgumentsTests
{
    [Fact(DisplayName = "文本读取把标量与其他值转成文本")]
    public void Text_reads_scalars_and_other_values_as_text()
    {
        ToolArguments arguments = Arguments("""{"city": "北京", "passed": true, "count": 3}""");

        Assert.Equal("北京", arguments.Text(new ToolName("city")));
        Assert.Equal("True", arguments.Text(new ToolName("passed")));
        Assert.Equal("3", arguments.Text(new ToolName("count")));
        Assert.Null(arguments.Text(new ToolName("missing")));
    }

    [Fact(DisplayName = "JSON 读取对象字符串与缺失值")]
    public void Json_reads_objects_strings_and_missing_values()
    {
        ToolArguments arguments = Arguments("""{"payload":{"text":"完成","trigger":true},"note":"空"}""");

        Assert.Equal("""{"text":"完成","trigger":true}""", arguments.Json(new ToolName("payload")));
        Assert.Equal("空", arguments.Json(new ToolName("note")));
        Assert.Null(arguments.Json(new ToolName("missing")));
    }

    [Fact(DisplayName = "JSON 写入把 CLR 字典写成对象")]
    public void Json_writes_clr_dictionaries_as_objects()
    {
        ToolArguments arguments = new(new Dictionary<string, object?>
        {
            ["payload"] = new Dictionary<string, object?>
            {
                ["text"] = "完成",
                ["trigger"] = true,
            },
        });

        string json = Assert.IsType<string>(arguments.Json(new ToolName("payload")));

        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal("完成", document.RootElement.GetProperty("text").GetString());
        Assert.True(document.RootElement.GetProperty("trigger").GetBoolean());
    }

    [Fact(DisplayName = "JSON 读取数字与数组原样保留")]
    public void Json_reads_numbers_and_arrays_verbatim()
    {
        ToolArguments arguments = Arguments("""{"count":3,"files":["甲","乙"]}""");

        Assert.Equal("3", arguments.Json(new ToolName("count")));
        Assert.Equal("""["甲","乙"]""", arguments.Json(new ToolName("files")));
    }

    [Fact(DisplayName = "JSON 写入把 CLR 列表写成数组")]
    public void Json_writes_clr_lists_as_arrays()
    {
        ToolArguments arguments = new(new Dictionary<string, object?>
        {
            ["files"] = new List<object?> { "甲", 2 },
        });

        string json = Assert.IsType<string>(arguments.Json(new ToolName("files")));

        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal("甲", document.RootElement[0].GetString());
        Assert.Equal(2, document.RootElement[1].GetInt32());
    }

    [Fact(DisplayName = "布尔读取接受布尔并解析文本形式")]
    public void Flag_reads_bool_and_parses_text_forms()
    {
        ToolArguments arguments = Arguments("""{"yes": true, "no": "false", "city": "北京", "none": null}""");

        Assert.True(arguments.Flag(new ToolName("yes")));
        Assert.False(arguments.Flag(new ToolName("no")));
        Assert.Null(arguments.Flag(new ToolName("city")));
        Assert.Null(arguments.Flag(new ToolName("none")));
        Assert.Null(arguments.Flag(new ToolName("missing")));
    }

    [Fact(DisplayName = "CLR 值按 JSON 值方式读取")]
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

    [Fact(DisplayName = "参数默认是字符串且非必填")]
    public void Parameters_default_to_string_and_not_required()
    {
        ToolParameter parameter = new(new ToolName("city"), "城市的中文名称");

        Assert.False(parameter.Flag);
        Assert.False(parameter.Required);
        Assert.False(parameter.List);
        Assert.False(parameter.Path);
        Assert.Null(parameter.PathAccess);
    }

    [Fact(DisplayName = "列表读取数组与单个字符串")]
    public void List_reads_arrays_and_single_strings()
    {
        ToolArguments arguments = Arguments("""{"files": ["a", "b", "", null], "single": "x", "none": null}""");

        IReadOnlyList<string>? files = arguments.List(new ToolName("files"));
        Assert.NotNull(files);
        Assert.Equal(["a", "b", "", ""], files);
        Assert.Equal(["x"], arguments.List(new ToolName("single")));
        Assert.Null(arguments.List(new ToolName("none")));
        Assert.Null(arguments.List(new ToolName("missing")));
    }

    private static readonly string[] values = ["a", "b"];

    [Fact(DisplayName = "列表读取 CLR 字符串数组")]
    public void List_reads_clr_string_arrays()
    {
        ToolArguments arguments = new(new Dictionary<string, object?>
        {
            ["files"] = values,
            ["single"] = "x",
        });

        IReadOnlyList<string>? files = arguments.List(new ToolName("files"));
        Assert.NotNull(files);
        Assert.Equal(["a", "b"], files);
        Assert.Equal(["x"], arguments.List(new ToolName("single")));
    }

    /// <summary>实参按模型给出的 JSON 值构造，值各自持有，不随文档释放失效。</summary>
    private static ToolArguments Arguments(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        return new ToolArguments(document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value.Clone()));
    }
}

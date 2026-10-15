using System.Text.Json;
using Kuroe.Shared.Executions.Tools;
using Microsoft.Extensions.AI;
using Xunit;

namespace Kuroe.Flow.Node.Llm.Json.Tests;

/// <summary>工具函数的 JSON 承载：参数 Schema 的形态与实参文本的写出。</summary>
public sealed class ToolJsonTests
{
    private static readonly ToolJson Json = new();

    [Fact]
    public void Schema_declares_text_parameters_and_required_entries()
    {
        JsonElement schema = Json.Schema(
        [
            new ToolParameter(new ToolName("files"), "要处理的文件", Required: true),
            new ToolParameter(new ToolName("dry"), "只预演", Flag: true),
        ]);

        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.Equal("string", schema.GetProperty("properties").GetProperty("files").GetProperty("type").GetString());
        Assert.Equal("要处理的文件", schema.GetProperty("properties").GetProperty("files").GetProperty("description").GetString());
        Assert.Equal("boolean", schema.GetProperty("properties").GetProperty("dry").GetProperty("type").GetString());
        Assert.Equal(["files"], schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public void Schema_declares_list_parameters_as_string_arrays()
    {
        JsonElement schema = Json.Schema([new ToolParameter(new ToolName("files"), "要处理的文件", List: true)]);

        JsonElement parameter = schema.GetProperty("properties").GetProperty("files");
        Assert.Equal("array", parameter.GetProperty("type").GetString());
        Assert.Equal("string", parameter.GetProperty("items").GetProperty("type").GetString());
        Assert.False(schema.TryGetProperty("required", out _));
    }

    [Fact]
    public void Describe_writes_json_values_verbatim_and_other_values_as_text()
    {
        using JsonDocument document = JsonDocument.Parse("""["甲", "乙"]""");
        AIFunctionArguments arguments = new(new Dictionary<string, object?>
        {
            ["files"] = document.RootElement.Clone(),
            ["mode"] = true,
            ["note"] = "空",
        });

        string text = Json.Describe(arguments);

        Assert.Contains("空", text);
        using JsonDocument rendered = JsonDocument.Parse(text);
        Assert.Equal(
            ["甲", "乙"],
            rendered.RootElement.GetProperty("files").EnumerateArray().Select(item => item.GetString()));
        Assert.True(rendered.RootElement.GetProperty("mode").GetBoolean());
        Assert.Equal("空", rendered.RootElement.GetProperty("note").GetString());
    }
}
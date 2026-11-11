using ErrorOr;
using Kuroe.Flow.Compilation.Shared;
using Xunit;

namespace Kuroe.Flow.Json.Tests;

/// <summary>节点库文件的 JSON 格式：文本到节点定义的映射与写回保真。</summary>
public sealed class JsonNodeFormatTests
{
    private static readonly JsonNodeFormat Format = new();

    [Fact]
    public void Read_maps_definitions_and_rejects_references()
    {
        IReadOnlyList<NodeDefinitionDocument> nodes = Ok(Format.Read(Nodes));

        NodeDefinitionDocument definition = Assert.Single(nodes);
        Assert.Equal("计划", definition.Name);
        Assert.Equal("Llm", definition.Kind);
        Assert.Equal("写出计划", Assert.IsType<FlowValue.Text>(definition.Parameters.Values["Prompt"]).Value);

        ErrorOr<IReadOnlyList<NodeDefinitionDocument>> reference =
            Format.Read("""{ "Nodes": [ { "Label": "副本", "Use": "计划" } ] }""");
        Assert.True(reference.IsError);
        Assert.Contains(reference.ErrorsOrEmptyList,
            error => error.Description.Contains("节点库定义不能是引用。"));
    }

    [Fact]
    public void Read_rejects_the_flows_field()
    {
        ErrorOr<IReadOnlyList<NodeDefinitionDocument>> document =
            Format.Read("""{ "Flows": [ { "Name": "默认" } ] }""");

        Assert.True(document.IsError);
        Assert.Contains(document.ErrorsOrEmptyList,
            error => error.Description.Contains("流程写在 flows.json", StringComparison.Ordinal));
    }

    [Fact]
    public void Write_keeps_parameters_and_round_trips()
    {
        IReadOnlyList<NodeDefinitionDocument> nodes = Ok(Format.Read(Nodes));
        string text = Ok(Format.Write(nodes));

        Assert.Contains("\"Prompt\": \"写出计划\"", text);
        Assert.Contains("\"Nodes\"", text);
        Assert.DoesNotContain("\"Flows\"", text);
        Assert.DoesNotContain(": null", text);
        Assert.Equal(text, Ok(Format.Write(Ok(Format.Read(text)))));
    }

    [Fact]
    public void Read_and_write_keep_boolean_parameters()
    {
        IReadOnlyList<NodeDefinitionDocument> nodes = Ok(Format.Read("""
            {
              "Nodes": [
                { "Name": "构建", "Kind": "Command", "Parameters": { "Program": "dotnet", "FailOnNonZeroExitCode": true } }
              ]
            }
            """));

        NodeDefinitionDocument definition = Assert.Single(nodes);
        Assert.True(Assert.IsType<FlowValue.Boolean>(definition.Parameters.Values["FailOnNonZeroExitCode"]).Value);
        Assert.Contains("\"FailOnNonZeroExitCode\": true", Ok(Format.Write(nodes)));
    }

    private static T Ok<T>(ErrorOr<T> result)
    {
        Assert.False(result.IsError, string.Join("；", result.ErrorsOrEmptyList.Select(error => error.Description)));

        return result.Value;
    }

    private const string Nodes = """
        {
          "Nodes": [
            { "Name": "计划", "Kind": "Llm", "Parameters": { "Prompt": "写出计划" }, "Outputs": [{ "Name": "计划", "Type": "Text" }] }
          ]
        }
        """;
}
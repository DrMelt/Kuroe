using ErrorOr;
using Kuroe.Flow.Compiler.Shared;
using Xunit;

namespace Kuroe.Flow.Json.Tests;

/// <summary>流程文件的 JSON 格式：文本到描述文档的映射与写回保真。</summary>
public sealed class FlowDocumentFormatTests
{
    private static readonly JsonFlowFormat Format = new();

    [Fact]
    public void Read_maps_definitions_and_references_to_the_document()
    {
        FlowDocument document = Ok(Format.Read(Flow));
        NodeDefinitionDocument definition = Assert.IsType<NodeDefinitionDocument>(Assert.Single(document.Nodes));

        Assert.Equal("计划", definition.Name);
        Assert.Equal("Llm", definition.Kind);
        Assert.Equal("写出计划", Assert.IsType<FlowValue.Text>(definition.Parameters["Prompt"]).Value);

        NodeDefinitionDocument root = Assert.IsType<NodeDefinitionDocument>(Assert.Single(document.Flows).Root);
        NodeReferenceDocument reference = Assert.IsType<NodeReferenceDocument>(Assert.Single(root.Nodes!));
        Assert.Equal("副本", reference.Label);
        Assert.Equal("计划", reference.Use);
        Assert.Equal("执行者", reference.Model);
    }

    [Fact]
    public void Write_keeps_parameters_and_runtime_model()
    {
        FlowDocument document = Ok(Format.Read(Flow));
        string text = Ok(Format.Write(document));

        Assert.Contains("\"Prompt\": \"写出计划\"", text);
        Assert.Contains("\"Runtime\": true", text);
        Assert.Contains("\"Label\": \"副本\"", text);
        Assert.DoesNotContain("\": null", text);

        Assert.Equal(text, Ok(Format.Write(Ok(Format.Read(text)))));
    }

    [Fact]
    public void Read_rejects_a_reference_with_name_and_without_label()
    {
        ErrorOr<FlowDocument> document = Format.Read("""
            { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
              { "Name": "整体", "Kind": "Container", "Nodes": [ { "Name": "副本", "Use": "计划" } ] }
            ] } ] }
            """);

        Assert.True(document.IsError);
        Assert.Contains(document.ErrorsOrEmptyList,
            error => error.Description.Contains("被使用的节点写 Label 区分，不写 Name。"));
    }

    private static T Ok<T>(ErrorOr<T> result)
    {
        Assert.False(result.IsError, string.Join("；", result.ErrorsOrEmptyList.Select(error => error.Description)));

        return result.Value;
    }

    private const string Flow = """
        {
          "Nodes": [
            { "Name": "计划", "Kind": "Llm", "Prompt": "写出计划", "Outputs": [{ "Name": "计划", "Type": "Text" }] }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" }, { "Name": "运行时", "Runtime": true } ],
              "Start": { "Outputs": [ { "Name": "目标", "Type": "Text" } ] },
              "Nodes": [
                { "Name": "整体", "Kind": "Container", "Nodes": [
                  { "Label": "副本", "Use": "计划", "Model": "执行者", "From": [ { "目标": "Start@目标" } ] }
                ] }
              ]
            }
          ]
        }
        """;
}
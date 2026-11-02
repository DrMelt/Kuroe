using System.Text.Json;
using ErrorOr;
using Kuroe.Flow.Compilation.Shared;
using Kuroe.Flow.Json;

namespace Kuroe.Flow.Compilation.Integration.Tests;

/// <summary>测试用的文档读取：把一份同时含 Nodes 与 Flows 的 JSON 按顶层字段拆开，分别经节点库与流程格式解析后组合。</summary>
internal static class FlowDocumentSource
{
    private static readonly JsonNodeFormat Nodes = new();
    private static readonly JsonFlowFormat Flows = new();

    /// <summary>读入测试文档，任一部分不合法时返回全部错误。</summary>
    public static ErrorOr<FlowDocument> Read(string json)
    {
        (string nodesText, string flowsText) = Split(json);

        ErrorOr<IReadOnlyList<NodeDefinitionDocument>> nodes = Nodes.Read(nodesText);
        ErrorOr<IReadOnlyList<FlowDocumentFlow>> flows = Flows.Read(flowsText);
        List<Error> errors =
        [
            .. nodes.ErrorsOrEmptyList,
            .. flows.ErrorsOrEmptyList,
        ];

        return errors.Count > 0 ? errors : new FlowDocument(nodes.Value, flows.Value);
    }

    /// <summary>只写出流程部分，供流程结构的读写往返用例使用。</summary>
    public static ErrorOr<string> WriteFlows(FlowDocument document) => Flows.Write(document.Flows);

    private static (string Nodes, string Flows) Split(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        string nodes = Section(document.RootElement, "Nodes");
        string flows = Section(document.RootElement, "Flows");

        return (nodes, flows);
    }

    private static string Section(JsonElement root, string name)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return $"{{\"{name}\":{property.Value.GetRawText()}}}";
                }
            }
        }

        return name == "Nodes" ? """{"Nodes":[]}""" : """{"Flows":[]}""";
    }
}
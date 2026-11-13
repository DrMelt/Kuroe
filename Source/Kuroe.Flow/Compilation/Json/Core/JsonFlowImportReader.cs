using System.Text.Json;
using ErrorOr;
using Kuroe.Flow.Compilation.Shared;
using Kuroe.Flow.Json.Shared;

namespace Kuroe.Flow.Json;

/// <summary>导入文件的 JSON 判定：顶层只写 Nodes 时按节点库读取，只写 Flows 时按流程读取。</summary>
public sealed class JsonFlowImportReader(INodeFormatReader nodes, IFlowFormatReader flows) : IFlowImportReader
{
    /// <summary>读入导入文件。字段名大小写不敏感，允许注释与尾随逗号。</summary>
    public ErrorOr<FlowImportContent> Read(string text)
    {
        bool hasNodes;
        bool hasFlows;
        try
        {
            using JsonDocument document = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Error.Validation(FlowErrorCodes.FlowFormat, "导入文件必须是 JSON 对象。");
            }

            hasNodes = HasProperty(document.RootElement, "Nodes");
            hasFlows = HasProperty(document.RootElement, "Flows");
        }
        catch (JsonException ex)
        {
            return Error.Validation(FlowErrorCodes.FlowFormat, $"导入文件不是合法 JSON：{ex.Message}");
        }

        if (hasNodes == hasFlows)
        {
            return Error.Validation(
                FlowErrorCodes.FlowFormat,
                hasNodes ? "导入文件只能包含 Nodes 或 Flows 之一。" : "导入文件必须包含 Nodes 或 Flows。");
        }

        if (hasNodes)
        {
            ErrorOr<IReadOnlyList<NodeDefinitionDocument>> read = nodes.Read(text);

            return read.IsError
                ? read.ErrorsOrEmptyList
                : new FlowImportContent(FlowImportKind.Nodes, read.Value, []);
        }

        ErrorOr<IReadOnlyList<FlowDocumentFlow>> readFlows = flows.Read(text);

        return readFlows.IsError
            ? readFlows.ErrorsOrEmptyList
            : new FlowImportContent(FlowImportKind.Flows, [], readFlows.Value);
    }

    private static bool HasProperty(JsonElement root, string name) =>
        root.EnumerateObject().Any(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase));
}
using System.Text.Json;
using Kuroe.Flow.Compilation.Shared;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Json.Files;

/// <summary>描述文档到文件形状的写出：只映射字段，不依赖模型与类型目录。</summary>
static class FlowFileWriter
{
    /// <summary>把节点定义写成节点库文件形状。</summary>
    public static NodeFileDto WriteNodes(IReadOnlyList<NodeDefinitionDocument> nodes) => new()
    {
        Nodes = [.. nodes.Select(WriteDefinition)],
    };

    /// <summary>把流程描述写成流程文件形状。</summary>
    public static FlowFileDto WriteFlows(IReadOnlyList<FlowDocumentFlow> flows) => new()
    {
        Flows = [.. flows.Select(WriteFlow)],
    };

    private static FlowDto WriteFlow(FlowDocumentFlow flow) => new()
    {
        Name = flow.Name,
        Description = flow.Description,
        Start = new StartDto { Outputs = WritePorts(flow.StartOutputs) },
        HostStartInputs = flow.HostStartInputs.Count > 0
            ? new Dictionary<string, string>(flow.HostStartInputs)
            : null,
        Root = flow.Root is null ? null : WriteNode(flow.Root),
    };

    private static NodeDto WriteNode(NodeDocument node) => node switch
    {
        NodeReferenceDocument reference => WriteReference(reference),
        NodeDefinitionDocument definition => WriteDefinition(definition),
        _ => throw new InvalidOperationException($"不认识的节点描述 {node.GetType().Name}。"),
    };

    private static NodeDto WriteDefinition(NodeDefinitionDocument node) => new()
    {
        Name = node.Name,
        Kind = node.Kind,
        Inputs = WriteInputs(node.Inputs),
        Outputs = WritePorts(node.Outputs),
        From = WriteFrom(node.From),
        MaxExecutions = node.MaxExecutions,
        Bindings = WriteBindings(node.Bindings),
        Emission = node.Emission?.ToString(),
        Nodes = node.Nodes is { Count: > 0 } children ? [.. children.Select(WriteNode)] : null,
        Parameters = WriteParameters(node.Parameters),
    };

    private static NodeDto WriteReference(NodeReferenceDocument node) => new()
    {
        Label = node.Label,
        Use = node.Use,
        From = WriteFrom(node.From),
        MaxExecutions = node.MaxExecutions,
        Bindings = WriteBindings(node.Bindings),
        Emission = node.Emission?.ToString(),
    };

    private static List<JsonElement>? WritePorts(IReadOnlyList<PortDocument> ports)
    {
        if (ports.Count == 0)
        {
            return null;
        }

        return
        [
            .. ports.Select(port => JsonSerializer.SerializeToElement(
                new PortEntryDto
                {
                    Name = port.Name,
                    Type = port.Type?.Name,
                    Mode = port.Mode?.ToString(),
                    Description = port.Description,
                    Value = port.Value,
                    Ref = port.Ref,
                    Default = port.Default,
                },
                FlowJson.Default.PortEntryDto)),
        ];
    }

    private static List<JsonElement>? WriteInputs(IReadOnlyList<InputPortDocument> inputs)
    {
        if (inputs.Count == 0)
        {
            return null;
        }

        return
        [
            .. inputs.Select(input => JsonSerializer.SerializeToElement(
                new PortEntryDto
                {
                    Name = input.Name,
                    Type = input.Type?.Name,
                    Mode = input.Mode?.ToString(),
                    Description = input.Description,
                    Default = input.Default,
                    Role = input.Role?.ToString(),
                },
                FlowJson.Default.PortEntryDto)),
        ];
    }

    private static List<JsonElement>? WriteFrom(IReadOnlyList<FromGroupDocument> groups)
    {
        if (groups.Count == 0)
        {
            return null;
        }

        return
        [
            .. groups.Select(group => JsonSerializer.SerializeToElement(
                group.Links.ToDictionary(link => link.Input, WriteFromSource),
                FlowJson.Default.DictionaryStringJsonElement)),
        ];
    }

    /// <summary>写一条 From 来源：正向条件写引用文本，翻转条件写带 Ref 与 Condition 的对象。</summary>
    private static JsonElement WriteFromSource(FromLinkDocument link) =>
        link.Condition switch
        {
            null or TriggerCondition.Positive => JsonSerializer.SerializeToElement(link.Ref, FlowJson.Default.String),
            { } condition => JsonSerializer.SerializeToElement(
                new BindingEntryDto { Ref = link.Ref, Condition = condition.ToString() },
                FlowJson.Default.BindingEntryDto),
        };

    private static Dictionary<string, JsonElement>? WriteBindings(IReadOnlyDictionary<string, string>? bindings) =>
        bindings is { Count: > 0 }
            ? bindings.ToDictionary(
                entry => entry.Key,
                entry => JsonSerializer.SerializeToElement(entry.Value, FlowJson.Default.String))
            : null;

    private static Dictionary<string, JsonElement>? WriteParameters(IReadOnlyDictionary<string, FlowValue> parameters)
    {
        if (parameters.Count == 0)
        {
            return null;
        }

        return parameters.ToDictionary(entry => entry.Key, entry => ToElement(entry.Value));
    }

    private static JsonElement ToElement(FlowValue value)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            WriteValue(writer, value);
        }

        using JsonDocument document = JsonDocument.Parse(stream.ToArray());

        return document.RootElement.Clone();
    }

    private static void WriteValue(Utf8JsonWriter writer, FlowValue value)
    {
        switch (value)
        {
            case FlowValue.Text text:
                writer.WriteStringValue(text.Value);
                break;

            case FlowValue.Number number:
                writer.WriteRawValue(number.Raw);
                break;

            case FlowValue.Boolean boolean:
                writer.WriteBooleanValue(boolean.Value);
                break;

            case FlowValue.Null:
                writer.WriteNullValue();
                break;

            case FlowValue.Array array:
                writer.WriteStartArray();
                foreach (FlowValue item in array.Items)
                {
                    WriteValue(writer, item);
                }

                writer.WriteEndArray();
                break;

            case FlowValue.Object obj:
                writer.WriteStartObject();
                foreach ((string name, FlowValue member) in obj.Members)
                {
                    writer.WritePropertyName(name);
                    WriteValue(writer, member);
                }

                writer.WriteEndObject();
                break;
        }
    }
}
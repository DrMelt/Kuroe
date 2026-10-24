using System.Text.Json;
using Kuroe.Flow.Compiler.Shared;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Json.Files;

/// <summary>描述文档到文件形状的写出：只映射字段，不依赖模型与类型目录。</summary>
static class FlowDocumentWriter
{
    /// <summary>把描述文档写成文件形状。</summary>
    public static FlowFileDto Write(FlowDocument document) => new()
    {
        Nodes = [.. document.Nodes.Select(WriteDefinition)],
        Flows = [.. document.Flows.Select(WriteFlow)],
    };

    private static FlowDto WriteFlow(FlowDocumentFlow flow) => new()
    {
        Name = flow.Name,
        Description = flow.Description,
        Models =
        [
            .. flow.Models.Select(model => new ModelDto
            {
                Name = model.Name,
                Model = model.Model,
                Runtime = model.Runtime ? true : null,
            }),
        ],
        Start = new StartDto { Outputs = WritePorts(flow.StartOutputs, allowValue: true) },
        Nodes = flow.Root is null ? [] : [WriteNode(flow.Root)],
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
        Outputs = WritePorts(node.Outputs, allowValue: false),
        From = WriteFrom(node.From),
        Validate = WriteValidation(node.Validate),
        MaxExecutions = node.MaxExecutions,
        Model = node.Model,
        Models = node.ModelSlots is { Count: > 0 } slots
            ? JsonSerializer.SerializeToElement(new List<string>(slots), FlowJson.Default.ListString)
            : null,
        Nodes = node.Nodes is { Count: > 0 } children ? [.. children.Select(WriteNode)] : null,
        Parameters = WriteParameters(node.Parameters),
    };

    private static NodeDto WriteReference(NodeReferenceDocument node) => new()
    {
        Label = node.Label,
        Use = node.Use,
        From = WriteFrom(node.From),
        Validate = WriteValidation(node.Validate),
        MaxExecutions = node.MaxExecutions,
        In = node.In is { Count: > 0 } bindings ? new Dictionary<string, string>(bindings) : null,
        Model = node.Model,
        Models = node.Models is { Count: > 0 } models
            ? JsonSerializer.SerializeToElement(
                new Dictionary<string, string>(models), FlowJson.Default.DictionaryStringString)
            : null,
    };

    private static List<JsonElement>? WritePorts(IReadOnlyList<PortDocument> ports, bool allowValue)
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
                    Description = port.Description,
                    Value = allowValue ? port.Value : null,
                    Ref = port.Ref,
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
                    Description = input.Description,
                    Default = input.Default,
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
                group.Links.ToDictionary(link => link.Input, link => link.Ref),
                FlowJson.Default.DictionaryStringString)),
        ];
    }

    private static ValidationDto? WriteValidation(ValidationDocument? validation) =>
        validation is null ? null : new ValidationDto { Predicate = validation.Predicate, Argument = validation.Argument };

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
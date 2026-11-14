using System.Text.Json;
using Kuroe.Execution.Shared;

namespace Kuroe.Tool.Contract;

/// <summary>交回端口值的 JSON Schema：按当前执行的端口声明给出每个端口的字符串或对象形态。</summary>
internal static class ContractPortSchemas
{
    /// <summary>按需交回端口生成 valuesJson 的对象 schema。</summary>
    public static JsonElement Of(IReadOnlyList<ExecutionOutputPort> ports)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "object");
            writer.WriteString("description", "端口名到值的对象。每个声明的端口都必须提交，不能提交未声明的端口；触发位写在端口对象内。");
            writer.WriteStartObject("properties");
            foreach (ExecutionOutputPort port in ports)
            {
                writer.WritePropertyName(port.Name.Value);
                WritePort(writer, port);
            }

            writer.WriteEndObject();
            writer.WriteStartArray("required");
            foreach (ExecutionOutputPort port in ports)
            {
                writer.WriteStringValue(port.Name.Value);
            }

            writer.WriteEndArray();
            writer.WriteBoolean("additionalProperties", false);
            writer.WriteEndObject();
        }

        using JsonDocument document = JsonDocument.Parse(buffer.ToArray());

        return document.RootElement.Clone();
    }

    private static void WritePort(Utf8JsonWriter writer, ExecutionOutputPort port)
    {
        bool carriesText = CarriesText(port);
        if (!port.CarriesTrigger)
        {
            WriteTextPort(
                writer,
                port,
                carriesText ? PortValueFields.TextDescription : "交回空文本或端口名，内容不进入产出");
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("type", "object");
        WriteDescription(writer, port, carriesText ? "交回的文本与触发位" : "触发位");
        writer.WriteStartObject("properties");
        if (carriesText)
        {
            WriteField(writer, PortValueFields.Text, "string", PortValueFields.TextDescription);
        }

        WriteField(writer, PortValueFields.Trigger, "boolean", PortValueFields.TriggerDescription);
        writer.WriteEndObject();
        if (carriesText)
        {
            WriteRequired(writer, PortValueFields.Text, PortValueFields.Trigger);
        }
        else
        {
            WriteRequired(writer, PortValueFields.Trigger);
        }

        writer.WriteBoolean("additionalProperties", false);
        writer.WriteEndObject();
    }

    private static void WriteTextPort(Utf8JsonWriter writer, ExecutionOutputPort port, string fallback)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "string");
        WriteDescription(writer, port, fallback);
        writer.WriteEndObject();
    }

    private static void WriteField(Utf8JsonWriter writer, string name, string type, string description)
    {
        writer.WriteStartObject(name);
        writer.WriteString("type", type);
        writer.WriteString("description", description);
        writer.WriteEndObject();
    }

    private static void WriteDescription(Utf8JsonWriter writer, ExecutionOutputPort port, string fallback) =>
        writer.WriteString("description", port.Description is { Length: > 0 } description ? description : fallback);

    private static void WriteRequired(Utf8JsonWriter writer, params string[] names)
    {
        writer.WriteStartArray("required");
        foreach (string name in names)
        {
            writer.WriteStringValue(name);
        }

        writer.WriteEndArray();
    }

    /// <summary>端口是否承载文本内容，Signal 不承载内容。</summary>
    private static bool CarriesText(ExecutionOutputPort port) => port.Kind != ExecutionPortKind.Signal;
}
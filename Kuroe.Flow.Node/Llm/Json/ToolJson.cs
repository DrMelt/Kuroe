using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Kuroe.Flow.Node.Llm.Tools;
using Kuroe.Shared.Executions.Tools;
using Microsoft.Extensions.AI;

namespace Kuroe.Flow.Node.Llm.Json;

/// <summary>Llm 节点工具函数的 JSON 承载：参数的 JSON Schema 与一次调用的实参文本，由宿主在装配处注册。</summary>
public sealed class ToolJson : IToolJson
{
    /// <summary>调用参数的呈现方式：非 ASCII 字符不转义。</summary>
    private static readonly JsonWriterOptions Rendering = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>参数的 JSON Schema，随请求交给模型。</summary>
    public JsonElement Schema(IReadOnlyList<ToolParameter> parameters)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "object");
            writer.WriteStartObject("properties");
            foreach (ToolParameter parameter in parameters)
            {
                writer.WriteStartObject(parameter.Name.Value);
                if (parameter.List)
                {
                    writer.WriteString("type", "array");
                    writer.WriteStartObject("items");
                    writer.WriteString("type", "string");
                    writer.WriteEndObject();
                }
                else
                {
                    writer.WriteString("type", parameter.Flag ? "boolean" : "string");
                }

                writer.WriteString("description", parameter.Description);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            if (parameters.Any(parameter => parameter.Required))
            {
                writer.WriteStartArray("required");
                foreach (ToolParameter parameter in parameters.Where(parameter => parameter.Required))
                {
                    writer.WriteStringValue(parameter.Name.Value);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        return JsonDocument.Parse(buffer.ToArray()).RootElement.Clone();
    }

    /// <summary>实参写成 JSON 文本。</summary>
    public string Describe(AIFunctionArguments arguments)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer, Rendering))
        {
            writer.WriteStartObject();
            foreach ((string name, object? value) in arguments)
            {
                writer.WritePropertyName(name);
                WriteValue(writer, value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>JSON 值原样写出，其余值按文本写出。</summary>
    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;

            case JsonElement element:
                element.WriteTo(writer);
                break;

            case string text:
                writer.WriteStringValue(text);
                break;

            case bool flag:
                writer.WriteBooleanValue(flag);
                break;

            default:
                writer.WriteStringValue(value.ToString());
                break;
        }
    }
}
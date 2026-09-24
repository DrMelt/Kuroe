using System.Text.Json;

namespace Kuroe.Agent.Tools;

/// <summary>模型可调用的一个函数：名字、说明、参数与调用体。调用体把一段文本交回模型，拒绝也写成文本。</summary>
/// <remarks>登记函数声明，参数拼成 JSON Schema。</remarks>
public sealed class ToolFunction(
    string name,
    string description,
    IReadOnlyList<ToolParameter> parameters,
    Func<ToolArguments, string> invoke)
{

    /// <summary>函数名，宿主按它列出可用性。</summary>
    public string Name { get; } = name;

    /// <summary>给模型的说明。</summary>
    public string Description { get; } = description;

    /// <summary>调用体，实参由库解析后传入。</summary>
    public Func<ToolArguments, string> Invoke { get; } = invoke;

    /// <summary>参数的 JSON Schema，随请求交给模型。</summary>
    internal JsonElement Schema { get; } = SchemaOf(parameters);

    private static JsonElement SchemaOf(IReadOnlyList<ToolParameter> parameters)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "object");
            writer.WriteStartObject("properties");
            foreach (ToolParameter parameter in parameters)
            {
                writer.WriteStartObject(parameter.Name);
                writer.WriteString("type", parameter.Flag ? "boolean" : "string");
                writer.WriteString("description", parameter.Description);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            if (parameters.Any(parameter => parameter.Required))
            {
                writer.WriteStartArray("required");
                foreach (ToolParameter parameter in parameters.Where(parameter => parameter.Required))
                {
                    writer.WriteStringValue(parameter.Name);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        return JsonDocument.Parse(buffer.ToArray()).RootElement.Clone();
    }
}

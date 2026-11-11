using System.Text;
using System.Text.Json;

namespace Kuroe.Tool;

/// <summary>工具调用值的 JSON 文本：JsonElement 原样写出，object 值字典与 object 序列递归写成对象与数组，其余值写成对应标量或文本。</summary>
public static class ToolJsonText
{
    /// <summary>把一个工具调用值写成 JSON 文本。</summary>
    public static string Write(object? value)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            WriteValue(writer, value);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>把一个工具调用值写进已有的 JSON 写入器。</summary>
    public static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;

            case string text:
                writer.WriteStringValue(text);
                break;

            case bool flag:
                writer.WriteBooleanValue(flag);
                break;

            case byte number:
                writer.WriteNumberValue(number);
                break;

            case sbyte number:
                writer.WriteNumberValue(number);
                break;

            case short number:
                writer.WriteNumberValue(number);
                break;

            case ushort number:
                writer.WriteNumberValue(number);
                break;

            case int number:
                writer.WriteNumberValue(number);
                break;

            case uint number:
                writer.WriteNumberValue(number);
                break;

            case long number:
                writer.WriteNumberValue(number);
                break;

            case ulong number:
                writer.WriteNumberValue(number);
                break;

            case float number:
                writer.WriteNumberValue(number);
                break;

            case double number:
                writer.WriteNumberValue(number);
                break;

            case decimal number:
                writer.WriteNumberValue(number);
                break;

            case JsonElement element:
                element.WriteTo(writer);
                break;

            case IDictionary<string, object?> map:
                writer.WriteStartObject();
                foreach ((string key, object? item) in map)
                {
                    writer.WritePropertyName(key);
                    WriteValue(writer, item);
                }

                writer.WriteEndObject();
                break;

            case IEnumerable<object?> list:
                writer.WriteStartArray();
                foreach (object? item in list)
                {
                    WriteValue(writer, item);
                }

                writer.WriteEndArray();
                break;

            default:
                writer.WriteStringValue(value.ToString());
                break;
        }
    }
}
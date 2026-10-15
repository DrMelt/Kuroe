using System.Text.Json;
using System.Text.Json.Nodes;

namespace Kuroe.Flow.Shared.Files;

/// <summary>参数段的 JSON 值读写：不依赖序列化上下文，供各节点类型的参数形状使用。</summary>
public static class ParameterJson
{
    /// <summary>文本值的 JSON 元素。</summary>
    public static JsonElement Text(string value) => Element(JsonValue.Create(value));

    /// <summary>文本数组的 JSON 元素。</summary>
    public static JsonElement Texts(IEnumerable<string> values)
    {
        JsonArray array = [];
        foreach (string value in values)
        {
            array.Add((JsonNode?)JsonValue.Create(value));
        }

        return Element(array);
    }

    /// <summary>取文本值：未写或为空值时为 null，形状不符经 fail 报出并返回 null。</summary>
    public static string? ReadText(IReadOnlyDictionary<string, JsonElement> values, string name, Action<string> fail)
    {
        if (!values.TryGetValue(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            fail($"字段 {name} 必须是文本。");

            return null;
        }

        return value.GetString();
    }

    /// <summary>取文本数组：未写时为空集合，形状不符的条目经 fail 报出并跳过。</summary>
    public static IReadOnlyList<string> ReadTexts(IReadOnlyDictionary<string, JsonElement> values, string name, Action<string> fail)
    {
        if (!values.TryGetValue(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            fail($"字段 {name} 必须是文本数组。");

            return [];
        }

        List<string> result = [];
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                fail($"字段 {name} 的元素必须是文本。");

                continue;
            }

            result.Add(item.GetString()!);
        }

        return result;
    }

    /// <summary>把 JSON 节点转成不依赖临时文档的元素。</summary>
    private static JsonElement Element(JsonNode node)
    {
        using JsonDocument document = JsonDocument.Parse(node.ToJsonString());

        return document.RootElement.Clone();
    }
}
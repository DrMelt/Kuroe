using System.Text.Json;
using System.Text.Json.Nodes;
using Kuroe.Flow.Persistence.Serialization.Shared;

namespace Kuroe.Flow.Persistence.Json;

/// <summary>版本 13 存档到当前文档结构的迁移：流程定义与状态改为按实现解释的片段。</summary>
internal static class TaskRecordLegacyFormat
{
    /// <summary>把版本 13 的文档树迁移为当前文档。结构不合法时抛出 FormatException。</summary>
    public static TaskRecordDocument? Migrate(JsonElement root)
    {
        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(root.GetRawText());
        }
        catch (JsonException ex)
        {
            throw new FormatException($"存档 JSON 不合法：{ex.Message}");
        }

        if (parsed is not JsonObject document)
        {
            throw new FormatException("存档不是 JSON 对象。");
        }

        if (Find(document, "Task") is JsonObject task && Find(task, "Flow") is JsonObject flow)
        {
            Rename(flow, "GraphDefinition", "Definition");
            Rename(flow, "GraphState", "State");
            Rename(flow, "CodeDefinition", "Definition");
            Rename(flow, "CodeState", "State");
        }

        SetVersion(document);

        return document.Deserialize(TaskRecordJson.Default.TaskRecordDocument);
    }

    /// <summary>按名字忽略大小写取属性。</summary>
    private static JsonNode? Find(JsonObject node, string name)
    {
        foreach ((string key, JsonNode? value) in node)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>把旧属性名换成当前属性名，目标已存在时保留目标。</summary>
    private static void Rename(JsonObject node, string from, string to)
    {
        if (Find(node, to) is not null)
        {
            return;
        }

        string? source = null;
        JsonNode? value = null;
        foreach ((string key, JsonNode? candidate) in node)
        {
            if (string.Equals(key, from, StringComparison.OrdinalIgnoreCase))
            {
                source = key;
                value = candidate;
                break;
            }
        }

        if (source is null)
        {
            return;
        }

        node.Remove(source);
        node[to] = value?.DeepClone();
    }

    /// <summary>把版本写成当前版本。</summary>
    private static void SetVersion(JsonObject document)
    {
        string? existing = null;
        foreach ((string key, JsonNode? _) in document)
        {
            if (string.Equals(key, "Version", StringComparison.OrdinalIgnoreCase))
            {
                existing = key;
                break;
            }
        }

        if (existing is not null)
        {
            document.Remove(existing);
        }

        document["Version"] = TaskRecordSerializationVersion.Current;
    }
}
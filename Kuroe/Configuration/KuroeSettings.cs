using System.Text.Json.Nodes;
using ErrorOr;
using Kuroe.Agent;

namespace Kuroe.Configuration;

/// <summary>全部配置节的绑定结果。</summary>
public sealed record KuroeSettings
{
    public required AgentSettings Agent { get; init; }

    public static ErrorOr<KuroeSettings> From(JsonObject root)
    {
        JsonNode? node = Section(root, AgentSettings.SectionName);
        if (node is not null and not JsonObject)
        {
            return [Error.Validation($"{AgentSettings.SectionName}.Bind", $"{AgentSettings.SectionName} 节必须是对象。")];
        }

        ErrorOr<AgentSettings> agent = AgentSettings.From(node as JsonObject);

        return agent.IsError ? agent.ErrorsOrEmptyList : new KuroeSettings { Agent = agent.Value };
    }

    /// <summary>取该节的节点，不存在时返回 null，节名不区分大小写。</summary>
    private static JsonNode? Section(JsonObject root, string name)
    {
        foreach ((string key, JsonNode? value) in root)
        {
            if (key.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }
}
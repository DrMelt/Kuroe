using System.Reflection;
using System.Text.Json.Nodes;
using ErrorOr;
using Kuroe.Agent;

namespace Kuroe.Configuration;

/// <summary>全部配置节的绑定结果，也是用户层路径与键名的定义。</summary>
public sealed record KuroeSettings
{
    public required AgentSettings Agent { get; init; }

    /// <summary>把用户输入的路径解析为由属性名构成的规范路径。路径不对应已定义设置项时返回错误。</summary>
    public static ErrorOr<string> ResolvePath(string path)
    {
        string[] segments = path.Split(':');
        Type? type = typeof(KuroeSettings);

        for (int i = 0; i < segments.Length; i++)
        {
            if (type is null || Property(type, segments[i]) is not { } property)
            {
                return [SettingsErrors.Undefined(path)];
            }

            segments[i] = property.Name;
            type = IsSection(property.PropertyType) ? property.PropertyType : null;
        }

        return string.Join(':', segments);
    }

    /// <summary>把用户层树中的设置项键改写为属性名，未知键原样保留。同一设置项写成多个大小写变体时返回错误。</summary>
    public static ErrorOr<JsonObject> Normalize(JsonObject root) => Normalize(root, typeof(KuroeSettings), string.Empty);

    /// <summary>绑定全部节，输入须是 <see cref="Normalize"/> 处理过的树。</summary>
    public static ErrorOr<KuroeSettings> From(JsonObject root)
    {
        JsonNode? node = Section(root, AgentSettings.SectionName);
        if (node is not null and not JsonObject)
        {
            return [AgentErrors.Bind($"{AgentSettings.SectionName} 节必须是对象。")];
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

    private static ErrorOr<JsonObject> Normalize(JsonObject node, Type type, string path)
    {
        JsonObject normalized = [];

        foreach ((string key, JsonNode? value) in node)
        {
            if (Property(type, key) is not { } property)
            {
                normalized[key] = value?.DeepClone();
                continue;
            }

            string itemPath = path.Length == 0 ? property.Name : $"{path}:{property.Name}";
            if (normalized.ContainsKey(property.Name))
            {
                return [SettingsErrors.DuplicateKey(itemPath)];
            }

            if (value is JsonObject section && IsSection(property.PropertyType))
            {
                ErrorOr<JsonObject> child = Normalize(section, property.PropertyType, itemPath);
                if (child.IsError)
                {
                    return child.ErrorsOrEmptyList;
                }

                normalized[property.Name] = child.Value;
                continue;
            }

            normalized[property.Name] = value?.DeepClone();
        }

        return normalized;
    }

    private static PropertyInfo? Property(Type type, string name) =>
        type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

    /// <summary>属性类型是 Kuroe 定义的配置节时路径可以继续，其余类型是设置项。</summary>
    private static bool IsSection(Type type) => type.IsClass && type.Assembly == typeof(KuroeSettings).Assembly;
}
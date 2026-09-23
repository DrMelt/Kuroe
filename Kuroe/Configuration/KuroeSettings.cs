using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using ErrorOr;

namespace Kuroe.Configuration;

/// <summary>全部配置节的绑定结果，也是用户层路径与键名的定义。
/// 路径由公共实例属性的反射得出，因此类内部化后成员仍声明为 public。</summary>
internal sealed record KuroeSettings
{
    /// <summary>生效值缺失时的呈现文本。</summary>
    public const string NotSetText = "未设置";

    public required AgentSettings Agent { get; init; }

    /// <summary>按当前生效值列出各节与各设置项，路径与 <see cref="ResolvePath"/> 同源。</summary>
    public IReadOnlyList<SettingSection> Sections(UserSettingsStore store)
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.Instance;

        List<SettingSection> sections = [];
        foreach (PropertyInfo section in typeof(KuroeSettings).GetProperties(Declared))
        {
            if (!IsSection(section.PropertyType))
            {
                continue;
            }

            object? body = section.GetValue(this);
            List<SettingEntry> entries = [];

            foreach (PropertyInfo item in section.PropertyType.GetProperties(Declared))
            {
                string path = $"{section.Name}:{item.Name}";
                object? value = item.GetValue(body);
                string text = value is null ? NotSetText : Convert.ToString(value, CultureInfo.InvariantCulture)!;
                entries.Add(new SettingEntry(path, item.Name, text, store.TryGetValue(path) is not null));
            }

            sections.Add(new SettingSection(section.Name, entries));
        }

        return sections;
    }

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
            return [SettingsErrors.Bind($"{AgentSettings.SectionName} 节必须是对象。")];
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
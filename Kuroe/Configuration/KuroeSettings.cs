using System.Globalization;
using System.Text.Json.Nodes;
using ErrorOr;

namespace Kuroe.Configuration;

/// <summary>全部配置节的绑定结果。路径与设置项由 <see cref="SettingDefinitions"/> 给出。</summary>
internal sealed record KuroeSettings
{
    /// <summary>生效值缺失时的呈现文本。</summary>
    public const string NotSetText = "未设置";

    public required AgentSettings Agent { get; init; }

    /// <summary>按当前生效值列出各节与各设置项，路径与 <see cref="ResolvePath"/> 同源。</summary>
    public IReadOnlyList<SettingSection> Sections(UserSettingsStore store) =>
    [
        .. SettingDefinitions.All
            .GroupBy(definition => definition.Section)
            .Select(section => new SettingSection(section.Key,
            [
                .. section.Select(definition => new SettingEntry(definition.Path, definition.Name,
                    Text(definition.Value(this)), store.TryGetValue(definition.Path) is not null)),
            ])),
    ];

    /// <summary>把用户输入的路径解析为声明里的规范路径。路径不对应已定义设置项时返回错误。</summary>
    public static ErrorOr<string> ResolvePath(string path)
    {
        string[] segments = path.Split(':');

        if (segments.Length == 1 && SettingDefinitions.SectionName(segments[0]) is { } section)
        {
            return section;
        }

        if (segments.Length == 2 && SettingDefinitions.Find(segments[0], segments[1]) is { } definition)
        {
            return definition.Path;
        }

        return [SettingsErrors.Undefined(path)];
    }

    /// <summary>把用户层树中的设置项键改写为声明的项名，未知键原样保留。同一设置项写成多个大小写变体时返回错误。</summary>
    public static ErrorOr<JsonObject> Normalize(JsonObject root)
    {
        JsonObject normalized = [];

        foreach ((string key, JsonNode? value) in root)
        {
            if (SettingDefinitions.SectionName(key) is not { } section)
            {
                normalized[key] = value?.DeepClone();
                continue;
            }

            if (normalized.ContainsKey(section))
            {
                return [SettingsErrors.DuplicateKey(section)];
            }

            if (value is not JsonObject body)
            {
                normalized[section] = value?.DeepClone();
                continue;
            }

            ErrorOr<JsonObject> items = NormalizeSection(body, section);
            if (items.IsError)
            {
                return items.ErrorsOrEmptyList;
            }

            normalized[section] = items.Value;
        }

        return normalized;
    }

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

    /// <summary>把一节的键改写为声明的项名，未知键原样保留。</summary>
    private static ErrorOr<JsonObject> NormalizeSection(JsonObject node, string section)
    {
        JsonObject normalized = [];

        foreach ((string key, JsonNode? value) in node)
        {
            if (SettingDefinitions.Find(section, key)?.Name is not { } item)
            {
                normalized[key] = value?.DeepClone();
                continue;
            }

            if (normalized.ContainsKey(item))
            {
                return [SettingsErrors.DuplicateKey($"{section}:{item}")];
            }

            normalized[item] = value?.DeepClone();
        }

        return normalized;
    }

    /// <summary>生效值的呈现文本，未设置时用 <see cref="NotSetText"/>。</summary>
    private static string Text(object? value) =>
        value is null ? NotSetText : Convert.ToString(value, CultureInfo.InvariantCulture)!;
}

namespace Kuroe.Configuration.Model;

/// <summary>全部配置节的设置项。路径解析、用户层规范化与列出都以这张表为准，不反射配置类的属性。</summary>
internal static class SettingDefinitions
{
    /// <summary>全部设置项，顺序即列出顺序。</summary>
    internal static readonly IReadOnlyList<SettingDefinition> All =
    [
        Runtime(nameof(RuntimeSettings.Model), settings => settings.Runtime.Model),
        Runtime(nameof(RuntimeSettings.LogLevel), settings => settings.Runtime.LogLevel),
        Assistant(nameof(AssistantSettings.Language), settings => settings.Assistant.Language),
    ];

    /// <summary>按输入取规范节名，大小写不敏感，未定义的节返回 null。</summary>
    internal static string? SectionName(string name) =>
        All.Select(definition => definition.Section)
            .FirstOrDefault(section => string.Equals(section, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>按节名与项名取声明，大小写不敏感，未定义的组合返回 null。</summary>
    internal static SettingDefinition? Find(string section, string name) => All.FirstOrDefault(definition =>
        string.Equals(definition.Section, section, StringComparison.OrdinalIgnoreCase)
        && string.Equals(definition.Name, name, StringComparison.OrdinalIgnoreCase));

    private static SettingDefinition Runtime(string name, Func<KuroeSettings, object?> value) =>
        new(RuntimeSettings.SectionName, name, value);

    private static SettingDefinition Assistant(string name, Func<KuroeSettings, object?> value) =>
        new(AssistantSettings.SectionName, name, value);
}

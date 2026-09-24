namespace Kuroe.Configuration;

/// <summary>全部配置节的设置项。路径解析、用户层规范化与列出都以这张表为准，不反射配置类的属性。</summary>
internal static class SettingDefinitions
{
    /// <summary>全部设置项，顺序即列出顺序。</summary>
    internal static readonly IReadOnlyList<SettingDefinition> All =
    [
        Agent(nameof(AgentSettings.Model), settings => settings.Agent.Model),
        Agent(nameof(AgentSettings.SystemPrompt), settings => settings.Agent.SystemPrompt),
        Agent(nameof(AgentSettings.LogLevel), settings => settings.Agent.LogLevel),
        Agent(nameof(AgentSettings.Temperature), settings => settings.Agent.Temperature),
        Agent(nameof(AgentSettings.MaxOutputTokens), settings => settings.Agent.MaxOutputTokens),
        Agent(nameof(AgentSettings.MaxConcurrentRuns), settings => settings.Agent.MaxConcurrentRuns),
        Agent(nameof(AgentSettings.DefaultFlow), settings => settings.Agent.DefaultFlow),
        Agent(nameof(AgentSettings.MaxAttempts), settings => settings.Agent.MaxAttempts),
    ];

    /// <summary>按输入取规范节名，大小写不敏感，未定义的节返回 null。</summary>
    internal static string? SectionName(string name) =>
        All.Select(definition => definition.Section)
            .FirstOrDefault(section => string.Equals(section, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>按节名与项名取声明，大小写不敏感，未定义的组合返回 null。</summary>
    internal static SettingDefinition? Find(string section, string name) => All.FirstOrDefault(definition =>
        string.Equals(definition.Section, section, StringComparison.OrdinalIgnoreCase)
        && string.Equals(definition.Name, name, StringComparison.OrdinalIgnoreCase));

    private static SettingDefinition Agent(string name, Func<KuroeSettings, object?> value) =>
        new(AgentSettings.SectionName, name, value);
}

namespace Kuroe.Configuration;

/// <summary>一个配置节及其全部设置项。项的顺序即 <see cref="SettingDefinitions"/> 里的声明顺序。</summary>
public sealed record SettingSection(string Name, IReadOnlyList<SettingEntry> Entries);

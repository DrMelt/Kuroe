namespace Kuroe.Shared.Configuration;

/// <summary>一个配置节及其全部设置项，按声明顺序。</summary>
public sealed record SettingSection(string Name, IReadOnlyList<SettingEntry> Entries);

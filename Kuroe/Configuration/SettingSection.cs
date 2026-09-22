namespace Kuroe.Configuration;

/// <summary>一个配置节及其全部设置项。项的顺序由配置类的属性反射得出，不作保证。</summary>
public sealed record SettingSection(string Name, IReadOnlyList<SettingEntry> Entries);

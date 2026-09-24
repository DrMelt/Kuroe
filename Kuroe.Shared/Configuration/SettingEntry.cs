namespace Kuroe.Shared.Configuration;

/// <summary>一个设置项：规范路径、项名、当前生效值文本、用户层是否设置了该项。用户层设成与默认值相同的值时 FromUserLayer 仍为真。</summary>
public sealed record SettingEntry(string Path, string Name, string Value, bool FromUserLayer);

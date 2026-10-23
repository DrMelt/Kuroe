using Kuroe.Configuration;
using Kuroe.Shared.Configuration;

namespace Kuroe.Cli.Views;

/// <summary>生效配置与来源的纯文本呈现。</summary>
internal static class PlainSettingsView
{
    /// <summary>逐节列出设置项，标记各项是否来自用户层。</summary>
    public static void Print(CliOutput output, SettingsProvider settings)
    {
        output.Line($"用户层文件\t{settings.UserSettingsFile}");

        foreach (SettingSection section in settings.Sections)
        {
            output.Line();
            output.Line($"[{section.Name}]");
            output.Line("路径\t值\t来源");
            foreach (SettingEntry entry in section.Entries)
            {
                output.Line($"{entry.Path}\t{entry.Value}\t{(entry.FromUserLayer ? "用户层已设置" : "用户层未设置")}");
            }
        }
    }
}
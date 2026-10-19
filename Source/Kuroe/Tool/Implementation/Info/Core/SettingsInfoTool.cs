using System.Text;
using ErrorOr;
using Kuroe.Shared.Configuration;

namespace Kuroe.Tool.Info;

/// <summary>生效配置的信息查询工具。只读，不产生状态改动。</summary>
public sealed class SettingsInfoTool : ITool
{
    private readonly ISettingsView _settings;

    /// <summary>查看生效配置的函数定义。</summary>
    private static readonly ToolDefinition GetConfig = new(
        new ToolPath("info/GetConfig"),
        "查看生效配置：各节设置项的生效值与来源标记。",
        []);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>用生效配置的只读视图建立载体。</summary>
    public SettingsInfoTool(ISettingsView settings)
    {
        _settings = settings;
        Functions = [GetConfig.Bind(_ => Describe())];
    }

    /// <summary>配置各节的文本概况。</summary>
    private ErrorOr<string> Describe()
    {
        var text = new StringBuilder();
        text.AppendLine($"用户层文件：{_settings.UserSettingsFile}");

        foreach (SettingSection section in _settings.Sections)
        {
            text.AppendLine($"{section.Name}:");
            foreach (SettingEntry entry in section.Entries)
            {
                string source = entry.FromUserLayer ? "用户层已设置" : "用户层未设置";
                text.AppendLine($"  {entry.Name} · {entry.Value} · {source}");
            }
        }

        return text.ToString().TrimEnd();
    }
}

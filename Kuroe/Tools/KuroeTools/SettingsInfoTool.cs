using System.Text;
using ErrorOr;
using Kuroe.Configuration;
using Kuroe.Shared.Configuration;
using Kuroe.Shared.Executions.Tools;

namespace Kuroe.Tools.KuroeTools;

/// <summary>生效配置的信息查询工具。只读，不产生状态改动。</summary>
internal sealed class SettingsInfoTool : ITool
{
    private readonly SettingsProvider _settings;

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    public SettingsInfoTool(SettingsProvider settings)
    {
        _settings = settings;
        Functions =
        [
            new ToolFunction(new ToolName("GetConfig"),
                "查看生效配置：各节设置项的生效值与来源标记。",
                [], _ => Describe(), new ToolPath("info/GetConfig")),
        ];
    }

    /// <summary>配置各节的文本概况。</summary>
    private ErrorOr<string> Describe()
    {
        var text = new StringBuilder();
        text.AppendLine($"用户层文件：{_settings.UserSettingsFile}");

        foreach (SettingSection section in _settings.Sections())
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

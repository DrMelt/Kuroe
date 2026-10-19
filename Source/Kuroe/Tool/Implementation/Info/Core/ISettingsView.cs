using Kuroe.Shared.Configuration;

namespace Kuroe.Tool.Info;

/// <summary>生效配置的只读视图。由宿主提供。</summary>
public interface ISettingsView
{
    /// <summary>用户层偏好文件的绝对路径。</summary>
    string UserSettingsFile { get; }

    /// <summary>各配置节各设置项的生效值与来源。</summary>
    IReadOnlyList<SettingSection> Sections { get; }
}
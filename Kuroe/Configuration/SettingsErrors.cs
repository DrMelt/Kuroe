using ErrorOr;

namespace Kuroe.Configuration;

/// <summary>偏好配置域的错误构造。</summary>
public static class SettingsErrors
{
    public static Error UserFile(string path, string message) =>
        Error.Failure("Settings.UserFile", $"读取 {path} 失败：{message}");

    /// <summary>路径无法写入用户层。</summary>
    public static Error Path(string message) => Error.Validation("Settings.Path", message);

    /// <summary>改动后的配置无法绑定。</summary>
    public static Error Invalid() => Error.Validation("Settings.Invalid", "改动后的配置无效");

    /// <summary>路径不对应已定义的设置项。</summary>
    public static Error Undefined(string path) =>
        Error.Validation("Settings.Undefined", $"{path} 不是已定义的设置项。");

    /// <summary>同一设置项在用户层写成多个大小写变体。</summary>
    public static Error DuplicateKey(string path) =>
        Error.Validation("Settings.DuplicateKey", $"用户层中 {path} 出现多个大小写变体，请删除其中一个。");

    public static Error Write(string path, string message) =>
        Error.Failure("Settings.Write", $"写入 {path} 失败：{message}");
}
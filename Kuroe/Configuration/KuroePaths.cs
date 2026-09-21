namespace Kuroe.Configuration;

/// <summary>存储目录下的文件路径。目录由宿主给出，这里不做位置推断。</summary>
public sealed class KuroePaths
{
    private KuroePaths(string directory)
    {
        string root = Path.GetFullPath(directory);
        UserSettingsFile = Path.Combine(root, "settings.json");
        CatalogFile = Path.Combine(root, "catalog.json");
    }

    /// <summary>用户层偏好文件。</summary>
    public string UserSettingsFile { get; }

    /// <summary>目录文件，提供商、模型及其凭据都在其中。</summary>
    public string CatalogFile { get; }

    /// <summary>按存储目录组装文件路径，相对目录按进程当前目录解析。</summary>
    public static KuroePaths At(string directory) => new(directory);
}
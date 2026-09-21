namespace Kuroe.Configuration;

/// <summary>程序识别的环境变量与用户级配置文件的路径解析。</summary>
public sealed class KuroePaths
{
    /// <summary>配置层与用户目录覆盖共用的环境变量前缀。</summary>
    public const string EnvironmentPrefix = "KUROE_";

    /// <summary>覆盖用户目录的环境变量。</summary>
    public const string HomeVariable = EnvironmentPrefix + "HOME";

    private KuroePaths(string userDirectory)
    {
        UserSettingsFile = Path.Combine(userDirectory, "settings.json");
        CatalogFile = Path.Combine(userDirectory, "catalog.json");
    }

    /// <summary>用户层偏好文件。</summary>
    public string UserSettingsFile { get; }

    /// <summary>目录文件，提供商、模型及其凭据都在其中。</summary>
    public string CatalogFile { get; }

    public static KuroePaths Resolve()
    {
        string? home = Environment.GetEnvironmentVariable(HomeVariable);
        string directory = string.IsNullOrWhiteSpace(home)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kuroe")
            : home;

        return new KuroePaths(directory);
    }
}
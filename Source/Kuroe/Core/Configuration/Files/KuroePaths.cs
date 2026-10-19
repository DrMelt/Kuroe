namespace Kuroe.Configuration.Files;

/// <summary>存储目录下的文件路径。目录由宿主给出，这里不做位置推断。</summary>
public sealed class KuroePaths
{
    private KuroePaths(string directory)
    {
        string root = Path.GetFullPath(directory);
        WorkingDirectory = root;
        ConfigDirectory = Path.Combine(root, ".kuroe");
        UserSettingsFile = Path.Combine(ConfigDirectory, "settings.json");
        CatalogFile = Path.Combine(ConfigDirectory, "catalog.json");
        FlowsFile = Path.Combine(ConfigDirectory, "flows.json");
    }

    /// <summary>工作目录根，命令里的文件参数以此为基准。</summary>
    public string WorkingDirectory { get; }

    /// <summary>配置目录，各配置文件的所在目录。</summary>
    public string ConfigDirectory { get; }

    /// <summary>用户层偏好文件。</summary>
    public string UserSettingsFile { get; }

    /// <summary>目录文件，提供商、模型及其凭据都在其中。</summary>
    public string CatalogFile { get; }

    /// <summary>任务流程模板文件。</summary>
    public string FlowsFile { get; }

    /// <summary>按存储目录组装文件路径，相对目录按进程当前目录解析。</summary>
    public static KuroePaths At(string directory) => new(directory);
}


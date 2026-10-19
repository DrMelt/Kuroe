using Kuroe.Tool.Command.Model;

namespace Kuroe.Tool.Dotnet.Write;

/// <summary>添加包引用的命令定义。</summary>
internal static class AddPackage
{
    /// <summary>参数名：要改动的项目、解决方案或目录。</summary>
    private const string Project = "project";

    /// <summary>参数名：包名。</summary>
    private const string Package = "package";

    /// <summary>参数名：包版本。</summary>
    private const string Version = "version";

    /// <summary>函数定义与模板：dotnet add {project} package {package} [--version {version}]。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("dotnet/write/AddPackage"),
        "给项目添加包引用，project 是项目、解决方案或目录，package 是包名，version 是版本号。",
        [
            new CommandToolParameter(Project, "项目、解决方案或目录路径，缺省在项目工作目录里查找"),
            new CommandToolParameter(Package, "要添加的包名", Required: true),
            new CommandToolParameter(Version, "包版本号，缺省用可用的最新稳定版"),
        ],
        [
            new("dotnet", null),
            new("add", null),
            new("{project}", null),
            new("package", null),
            new("{package}", null),
            new("--version", Version),
            new("{version}", null),
        ],
        TimeoutSeconds: 300);
}
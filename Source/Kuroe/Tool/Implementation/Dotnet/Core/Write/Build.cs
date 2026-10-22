using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Dotnet.Write;

/// <summary>编译项目的命令定义。</summary>
internal static class Build
{
    /// <summary>参数名：要编译的项目、解决方案或目录。</summary>
    private const string Project = "project";

    /// <summary>参数名：生成配置。</summary>
    private const string Configuration = "configuration";

    /// <summary>函数定义与模板：dotnet build {project} [--configuration {configuration}]。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("dotnet/write/Build"),
        "编译项目，project 是项目、解决方案或目录，configuration 是生成配置如 Debug 或 Release。",
        [
            new CommandToolParameter(Project, "项目、解决方案或目录路径，缺省在项目工作目录里查找"),
            new CommandToolParameter(Configuration, "生成配置，如 Debug 或 Release，缺省用项目自身的配置"),
        ],
        [
            new("dotnet", null),
            new("build", null),
            new("{project}", null),
            new("--configuration", Configuration),
            new("{configuration}", null),
        ],
        TimeoutSeconds: 600,
        OutputLimit: 20000);
}
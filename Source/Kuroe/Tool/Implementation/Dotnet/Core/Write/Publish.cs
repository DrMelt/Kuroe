using Kuroe.Tool.Command.Model;

namespace Kuroe.Tool.Dotnet.Write;

/// <summary>发布项目的命令定义。</summary>
internal static class Publish
{
    /// <summary>参数名：要发布的项目、解决方案或目录。</summary>
    private const string Project = "project";

    /// <summary>参数名：生成配置。</summary>
    private const string Configuration = "configuration";

    /// <summary>函数定义与模板：dotnet publish {project} [--configuration {configuration}]。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolDefinition(
            new ToolPath("dotnet/write/Publish"),
            "发布项目到输出目录，project 是项目、解决方案或目录，configuration 是生成配置如 Debug 或 Release。",
            [
                new ToolParameter(new ToolName(Project), "项目、解决方案或目录路径，缺省在项目工作目录里查找"),
                new ToolParameter(new ToolName(Configuration), "生成配置，如 Debug 或 Release，缺省用项目自身的配置"),
            ]),
        [
            new("dotnet", null),
            new("publish", null),
            new("{project}", null),
            new("--configuration", Configuration),
            new("{configuration}", null),
        ],
        Directory: null,
        TimeoutSeconds: 600,
        OutputLimit: 20000);
}
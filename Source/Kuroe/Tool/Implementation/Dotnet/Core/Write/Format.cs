using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Dotnet.Write;

/// <summary>按代码风格整理源码的命令定义。</summary>
internal static class Format
{
    /// <summary>参数名：要整理的项目、解决方案或目录。</summary>
    private const string Project = "project";

    /// <summary>函数定义与模板：dotnet format {project}。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("dotnet/write/Format"),
        "按项目里的代码风格设置整理源码，project 是项目、解决方案或目录。",
        [new ToolParameter(new ToolName(Project), "项目、解决方案或目录路径，缺省在项目工作目录里查找", Path: true)],
        [new("dotnet", null), new("format", null), new("{project}", null)],
        TimeoutSeconds: 600,
        OutputLimit: 20000);
}
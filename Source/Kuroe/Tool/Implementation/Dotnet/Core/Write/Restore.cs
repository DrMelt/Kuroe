using Kuroe.Tool.Command.Model;

namespace Kuroe.Tool.Dotnet.Write;

/// <summary>还原依赖的命令定义。</summary>
internal static class Restore
{
    /// <summary>参数名：要还原的项目、解决方案或目录。</summary>
    private const string Project = "project";

    /// <summary>函数定义与模板：dotnet restore {project}。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolDefinition(
            new ToolPath("dotnet/write/Restore"),
            "还原项目的包依赖，project 是项目、解决方案或目录。",
            [new ToolParameter(new ToolName(Project), "项目、解决方案或目录路径，缺省在项目工作目录里查找")]),
        [new("dotnet", null), new("restore", null), new("{project}", null)],
        Directory: null,
        TimeoutSeconds: 600,
        OutputLimit: CommandToolTemplate.DefaultOutputLimit);
}
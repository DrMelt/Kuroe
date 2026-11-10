using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Dotnet.Write;

/// <summary>运行项目的命令定义。</summary>
internal static class Run
{
    /// <summary>参数名：要运行的项目或目录。</summary>
    private const string Project = "project";

    /// <summary>函数定义与模板：dotnet run {project}。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("dotnet/write/Run"),
        "运行项目；应用在超时前不结束会被终止。",
        [
            new ToolParameter(new ToolName(Project), "项目或目录路径，缺省在路径基准目录里查找", Path: true)
        ],
        [
        new("dotnet", null),
        new("run", null),
        new("{project}", null)
        ],
        TimeoutSeconds: 600,
        OutputLimit: 8000);
}
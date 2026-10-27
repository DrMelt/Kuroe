using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Dotnet.Write;

/// <summary>运行项目的命令定义。</summary>
internal static class Run
{
    /// <summary>参数名：要运行的项目或目录。</summary>
    private const string Project = "project";

    /// <summary>参数名：传给应用的参数列表。</summary>
    private const string Arguments = "arguments";

    /// <summary>函数定义与模板：dotnet run {project} [-- {arguments}]。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("dotnet/write/Run"),
        "运行项目，project 是项目或目录，arguments 是传给应用的参数；应用在超时前不结束会被终止。",
        [
            new ToolParameter(new ToolName(Project), "项目或目录路径，缺省在路径基准目录里查找", Path: true),
            new ToolParameter(new ToolName(Arguments), "传给应用的参数列表，缺省不给参数", List: true),
        ],
        [
            new("dotnet", null),
            new("run", null),
            new("{project}", null),
            new("--", Arguments),
            new("{arguments}", null),
        ],
        TimeoutSeconds: 600,
        OutputLimit: 20000);
}
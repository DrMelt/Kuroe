using Kuroe.Tool.Command.Model;

namespace Kuroe.Tool.Dotnet.Read;

/// <summary>列出项目包引用的命令定义。</summary>
internal static class ListPackages
{
    /// <summary>参数名：要查看的项目、解决方案或目录。</summary>
    private const string Project = "project";

    /// <summary>参数名：只列出有新版本的包。</summary>
    private const string Outdated = "outdated";

    /// <summary>函数定义与模板：dotnet list {project} package [--outdated]。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolDefinition(
            new ToolPath("dotnet/read/ListPackages"),
            "列出项目的顶层包引用，project 是项目、解决方案或目录，outdated 为真时只列出有新版本的包。",
            [
                new ToolParameter(new ToolName(Project), "项目、解决方案或目录路径，缺省在项目工作目录里查找"),
                new ToolParameter(new ToolName(Outdated), "只列出有新版本的包，布尔，缺省为假", Flag: true),
            ]),
        [new("dotnet", null), new("list", null), new("{project}", null), new("package", null), new("--outdated", Outdated)],
        Directory: null,
        TimeoutSeconds: 300,
        OutputLimit: CommandToolTemplate.DefaultOutputLimit);
}
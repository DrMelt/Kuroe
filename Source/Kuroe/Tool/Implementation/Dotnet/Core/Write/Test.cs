using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Dotnet.Write;

/// <summary>运行测试的命令定义。</summary>
internal static class Test
{
    /// <summary>参数名：测试所在的项目、解决方案或目录。</summary>
    private const string Project = "project";

    /// <summary>参数名：测试筛选表达式。</summary>
    private const string Filter = "filter";

    /// <summary>函数定义与模板：dotnet test {project} [--filter {filter}]。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("dotnet/write/Test"),
        "运行测试，project 是项目、解决方案或目录，filter 是测试筛选表达式。",
        [
            new ToolParameter(new ToolName(Project), "项目、解决方案或目录路径，缺省在项目工作目录里查找", Path: true),
            new ToolParameter(new ToolName(Filter), "测试筛选表达式，如 FullyQualifiedName~Kuroe，缺省运行全部测试"),
        ],
        [
            new("dotnet", null),
            new("test", null),
            new("{project}", null),
            new("--filter", Filter),
            new("{filter}", null),
        ],
        TimeoutSeconds: 600,
        OutputLimit: 20000);
}
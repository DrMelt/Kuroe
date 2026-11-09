using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Dotnet.Write;

/// <summary>使用 Microsoft.Testing.Platform 运行测试的命令定义。</summary>
internal static class TestMtp
{
    /// <summary>参数名：测试所在的项目。</summary>
    private const string Project = "project";

    /// <summary>参数名：测试所在的解决方案。</summary>
    private const string Solution = "solution";

    /// <summary>函数定义与模板：dotnet test [--project {project}] [--solution {solution}] --ignore-exit-code 8 --verbosity quiet -- --report-trx。
    /// 项目与解决方案二选一，都不给时由 dotnet test 在路径基准目录里查找；-- 之后的选项由 dotnet test 转给各测试应用。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("dotnet/write/TestMtp"),
        "使用 Microsoft.Testing.Platform 运行测试。",
        [
            new ToolParameter(new ToolName(Project), "测试项目路径，与 solution 二选一，缺省在路径基准目录里查找", Path: true),
            new ToolParameter(new ToolName(Solution), "测试所在解决方案的路径，与 project 二选一，缺省在路径基准目录里查找", Path: true),
        ],
        [
            new("dotnet", null),
            new("test", null),
            new("--project", Project),
            new("{project}", null),
            new("--solution", Solution),
            new("{solution}", null),
            new("--ignore-exit-code", null),
            new("8", null),
            new("--verbosity", null),
            new("quiet", null),
            new("--", null),
            new("--report-trx", null),
        ],
        TimeoutSeconds: 600,
        OutputLimit: 8000,
        Groups: [new ToolParameterGroup([new ToolName(Project), new ToolName(Solution)])]);
}
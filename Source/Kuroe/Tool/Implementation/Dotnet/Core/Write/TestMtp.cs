using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Dotnet.Write;

/// <summary>使用 Microsoft.Testing.Platform 运行测试的命令定义。</summary>
internal static class TestMtp
{
    /// <summary>参数名：测试所在的项目。</summary>
    private const string Project = "project";

    /// <summary>函数定义与模板：dotnet test [--project {project}] --ignore-exit-code 8 --verbosity quiet -- --report-trx。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("dotnet/write/TestMtp"),
        "使用 Microsoft.Testing.Platform 运行测试，project 是测试项目。",
        [
            new ToolParameter(new ToolName(Project), "测试项目路径，缺省在路径基准目录里查找", Path: true),
        ],
        [
            new("dotnet", null),
            new("test", null),
            new("--project", Project),
            new("{project}", null),
            new("--ignore-exit-code", null),
            new("8", null),
            new("--verbosity", null),
            new("quiet", null),
            new("--", null),
            new("--report-trx", null),
        ],
        TimeoutSeconds: 600,
        OutputLimit: 8000);
}
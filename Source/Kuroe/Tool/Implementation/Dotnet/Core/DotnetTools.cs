using Kuroe.Tool.Command;
using Kuroe.Tool.Command.Model;
using Kuroe.Tool.Dotnet.Read;
using Kuroe.Tool.Dotnet.Write;

namespace Kuroe.Tool.Dotnet;

/// <summary>内置 dotnet 工具面：只查环境的函数在 dotnet/read，会改动项目与产物的函数在 dotnet/write。
/// 全部函数都是命令工具定义，模板展开与进程执行复用命令面，命令在基准目录执行。</summary>
public static class DotnetTools
{
    /// <summary>全部函数的命令定义，按只读与改动分组，模板与参数在各函数文件里给出。</summary>
    public static IReadOnlyList<CommandToolDefinition> Definitions { get; } =
    [
        Info.Definition,
        ListSdks.Definition,
        ListPackages.Definition,
        NugetSources.Definition,
        Build.Definition,
        Test.Definition,
        Run.Definition,
        Restore.Definition,
        Format.Definition,
        Publish.Definition,
        AddPackage.Definition,
    ];

    /// <summary>把定义装配成在基准目录执行的工具载体。</summary>
    public static IReadOnlyList<ITool> Tools(string baseDirectory) =>
        [.. Definitions.Select(definition => new CommandTool(definition, baseDirectory))];
}
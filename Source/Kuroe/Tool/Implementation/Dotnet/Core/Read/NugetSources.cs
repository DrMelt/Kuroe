using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Dotnet.Read;

/// <summary>列出包源的命令定义。</summary>
internal static class NugetSources
{
    /// <summary>函数定义与模板：dotnet nuget list source，无参数。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("dotnet/read/NugetSources"),
        "列出配置的 NuGet 包源，命令在项目工作目录执行，读到工作目录及其上级的 NuGet 配置。",
        [],
        [new("dotnet", null), new("nuget", null), new("list", null), new("source", null)]);
}
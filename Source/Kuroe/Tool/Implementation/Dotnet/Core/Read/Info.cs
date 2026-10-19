using Kuroe.Tool.Command.Model;

namespace Kuroe.Tool.Dotnet.Read;

/// <summary>查看本机 .NET 环境的命令定义。</summary>
internal static class Info
{
    /// <summary>函数定义与模板：dotnet --info，无参数。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("dotnet/read/Info"),
        "查看本机 .NET 环境：SDK 与已装运行时的版本、安装位置与当前 RID。",
        [],
        [new("dotnet", null), new("--info", null)]);
}
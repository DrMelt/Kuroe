using Kuroe.Tool.Command.Model;

namespace Kuroe.Tool.Dotnet.Read;

/// <summary>列出已装 SDK 的命令定义。</summary>
internal static class ListSdks
{
    /// <summary>函数定义与模板：dotnet --list-sdks，无参数。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("dotnet/read/ListSdks"),
        "列出本机已安装的 SDK 版本与安装位置。",
        [],
        [new("dotnet", null), new("--list-sdks", null)]);
}
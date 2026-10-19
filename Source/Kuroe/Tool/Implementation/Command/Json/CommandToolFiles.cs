using Kuroe.Tool.Json;

namespace Kuroe.Tool.Command.Json;

/// <summary>命令工具文件形状的注册入口。</summary>
public static class CommandToolFiles
{
    /// <summary>把命令工具的文件形状注册进注册表。</summary>
    public static void RegisterInto(ToolFileRegistry files) => files.Register(new CommandToolFile());
}
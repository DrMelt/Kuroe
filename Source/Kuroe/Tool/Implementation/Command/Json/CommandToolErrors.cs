using ErrorOr;
using Kuroe.Shared;

namespace Kuroe.Tool.Command.Json;

/// <summary>命令工具条目的错误构造。</summary>
public static class CommandToolErrors
{
    /// <summary>命令工具定义本身不合规。</summary>
    public static Error Invalid(string tool, string message) =>
        Error.Validation(ErrorCodes.ToolInvalid, $"命令工具 {tool}：{message}");
}

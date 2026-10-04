using ErrorOr;
using Kuroe.Shared;

namespace Kuroe.Tools.CommandTools;

/// <summary>命令工具配置域的错误构造。</summary>
static class CommandToolErrors
{
    public static Error Read(string path, string message) =>
        Error.Failure(ErrorCodes.ToolRead, $"读取 {path} 失败：{message}");

    /// <summary>文件不是合法的 JSON 形状。</summary>
    public static Error Format(string message) => Error.Validation(ErrorCodes.ToolFormat, message);

    /// <summary>命令工具定义本身不合规。</summary>
    public static Error Invalid(string tool, string message) =>
        Error.Validation(ErrorCodes.ToolInvalid, $"命令工具 {tool}：{message}");

    /// <summary>命令工具与其他已注册工具重名。</summary>
    public static Error Duplicate(string tool) =>
        Error.Validation(ErrorCodes.ToolDuplicate, $"命令工具 {tool} 与已注册工具重名。");
}

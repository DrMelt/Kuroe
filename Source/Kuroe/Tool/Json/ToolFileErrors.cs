using ErrorOr;
using Kuroe.Shared;

namespace Kuroe.Tool.Json;

/// <summary>工具文件层的错误构造。</summary>
public static class ToolFileErrors
{
    /// <summary>读取工具文件失败。</summary>
    public static Error Read(string path, string message) =>
        Error.Failure(ErrorCodes.ToolRead, $"读取 {path} 失败：{message}");

    /// <summary>文件不是合法的 JSON 形状。</summary>
    public static Error Format(string message) => Error.Validation(ErrorCodes.ToolFormat, message);

    /// <summary>配置工具的路径与已注册工具重复。</summary>
    public static Error Duplicate(string path) =>
        Error.Validation(ErrorCodes.ToolDuplicate, $"配置工具 {path} 与已注册工具的路径重复。");
}
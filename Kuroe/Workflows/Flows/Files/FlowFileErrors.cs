using ErrorOr;
using Kuroe.Flow.Shared;

namespace Kuroe.Workflows.Flows.Files;

/// <summary>流程文件存取域的错误构造。</summary>
static class FlowFileErrors
{
    public static Error Read(string path, string message) =>
        Error.Failure("Flow.Read", $"读取 {path} 失败：{message}");

    public static Error Write(string path, string message) =>
        Error.Failure("Flow.Write", $"写入 {path} 失败：{message}");

    /// <summary>文件不是合法的 JSON 形状。</summary>
    public static Error Format(string message) => Error.Validation(FlowErrorCodes.FlowFormat, message);

    /// <summary>导入的文件参数不是合法路径。</summary>
    public static Error InvalidPath(string path, string message) =>
        Error.Validation(FlowErrorCodes.FlowInvalidPath, $"文件参数不是合法路径：{path}（{message}）");
}

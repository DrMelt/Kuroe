using ErrorOr;

namespace Kuroe.Shared.Executions.Tools;

/// <summary>工具调用拒绝与失败的错误构造，交回模型前由宿主渲染为文本。</summary>
public static class ToolErrors
{
    /// <summary>参数缺失、取值非法或参数指定的内容不被接受。</summary>
    public static Error Argument(string message) => Error.Validation(ErrorCodes.ToolArgument, message);

    /// <summary>路径解析越界，或目标是目录而不是文件。</summary>
    public static Error Path(string message) => Error.Validation(ErrorCodes.ToolPath, message);

    /// <summary>读取工作目录内文件失败。</summary>
    public static Error ReadFailed(string message) => Error.Failure(ErrorCodes.ToolReadFailed, message);

    /// <summary>写入工作目录内文件失败。</summary>
    public static Error WriteFailed(string message) => Error.Failure(ErrorCodes.ToolWriteFailed, message);

    /// <summary>命令无法启动或超时被终止。</summary>
    public static Error Execute(string message) => Error.Failure(ErrorCodes.ToolExecute, message);

    /// <summary>工具链路上不应出现的内部错误。</summary>
    public static Error Internal(string message) => Error.Unexpected(ErrorCodes.ToolInternal, message);
}

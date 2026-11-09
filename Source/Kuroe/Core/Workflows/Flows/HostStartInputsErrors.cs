using ErrorOr;
using Kuroe.Shared;

namespace Kuroe.Workflows.Flows;

/// <summary>宿主开始输入表的错误构造。表与流程的开始入口不一致时在任务提交阶段报出。</summary>
static class HostStartInputsErrors
{
    /// <summary>表项里的开始端口没有在流程的开始入口声明。</summary>
    public static Error UndeclaredPort(string flow, string port) =>
        Error.Validation(ErrorCodes.TaskStartInput, $"流程 {flow} 的开始入口没有声明开始输入表里的端口 {port}。");

    /// <summary>带固定值的开始端口不接受宿主给值。</summary>
    public static Error ConstantPort(string flow, string port) =>
        Error.Validation(ErrorCodes.TaskStartInput, $"流程 {flow} 的开始端口 {port} 带固定值，不能由宿主给值。");

    /// <summary>非 Text 开始端口不承载宿主给出的文本。</summary>
    public static Error NonTextPort(string flow, string port) =>
        Error.Validation(ErrorCodes.TaskStartInput, $"流程 {flow} 的开始端口 {port} 不是 Text 端口，不能由宿主给值。");

    /// <summary>表项给出的宿主输入名不受支持。</summary>
    public static Error UnsupportedInput(string flow, string port, string input) =>
        Error.Validation(ErrorCodes.TaskStartInput, $"流程 {flow} 的开始端口 {port} 引用了不受支持的宿主输入 {input}。");

    /// <summary>表文件里的流程名不是合法流程名。</summary>
    public static Error InvalidFlow(string flow) =>
        Error.Validation(ErrorCodes.TaskStartInput, $"开始输入表里的流程名 {flow} 不能为空。");

    /// <summary>表文件里的开始端口名不是合法端口名。</summary>
    public static Error InvalidPort(string flow, string port) =>
        Error.Validation(ErrorCodes.TaskStartInput, $"流程 {flow} 的开始输入表里 {port} 不是合法的开始端口名。");

    /// <summary>表文件不是合法 JSON 或结构不符。</summary>
    public static Error Format(string path, string message) =>
        Error.Validation(ErrorCodes.TaskStartInput, $"{path}：{message}");

    /// <summary>表文件读取失败。</summary>
    public static Error Read(string path, string message) =>
        Error.Failure(ErrorCodes.TaskStartInput, $"读取 {path} 失败：{message}");
}
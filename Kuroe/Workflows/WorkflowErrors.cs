using ErrorOr;
using Kuroe;

namespace Kuroe.Workflows;

/// <summary>流程配置域的错误构造。</summary>
static class WorkflowErrors
{
    public static Error Read(string path, string message) =>
        Error.Failure("Workflow.Read", $"读取 {path} 失败：{message}");

    public static Error Write(string path, string message) =>
        Error.Failure("Workflow.Write", $"写入 {path} 失败：{message}");

    /// <summary>文件不是合法的 JSON 形状。</summary>
    public static Error Format(string message) => Error.Validation(ErrorCodes.WorkflowFormat, message);

    public static Error Name(string name) =>
        Error.Validation(ErrorCodes.WorkflowName, $"{name} 不是合法的流程名。");

    /// <summary>导入的文件参数不是合法路径。</summary>
    public static Error InvalidPath(string path, string message) =>
        Error.Validation("Workflow.InvalidPath", $"文件参数不是合法路径：{path}（{message}）");

    public static Error Step(string flow, string step, string message) =>
        Error.Validation(ErrorCodes.WorkflowStep, $"流程 {flow} 的步骤 {step}：{message}");

    public static Error Body(string flow, string message) =>
        Error.Validation(ErrorCodes.WorkflowBody, $"流程 {flow}：{message}");

    /// <summary>条目数组的形状不合法，原因要能直接回给模型改正。</summary>
    public static Error Items(string reason) => Error.Validation(
        "Workflow.Items", $"{reason}。要提交对象数组，每项含 Title、Instruction、Acceptance。");

    /// <summary>没有该名称的流程模板。</summary>
    public static Error FlowNotFound(string name) =>
        Error.NotFound(ErrorCodes.WorkflowNotFound, $"没有名为 {name} 的流程。");
}

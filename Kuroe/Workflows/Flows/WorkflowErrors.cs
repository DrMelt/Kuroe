using ErrorOr;
using Kuroe.Shared;

namespace Kuroe.Workflows.Flows;

/// <summary>流程配置域的错误构造。</summary>
static class WorkflowErrors
{
    public static Error Read(string path, string message) =>
        Error.Failure("Workflow.Read", $"读取 {path} 失败：{message}");

    public static Error Write(string path, string message) =>
        Error.Failure("Workflow.Write", $"写入 {path} 失败：{message}");

    /// <summary>文件不是合法的 JSON 形状。</summary>
    public static Error Format(string message) => Error.Validation(ErrorCodes.WorkflowFormat, message);

    /// <summary>有流程没写名字。</summary>
    public static Error MissingName() => Error.Validation(ErrorCodes.WorkflowName, "有流程缺少名称。");

    /// <summary>导入的文件参数不是合法路径。</summary>
    public static Error InvalidPath(string path, string message) =>
        Error.Validation(ErrorCodes.WorkflowInvalidPath, $"文件参数不是合法路径：{path}（{message}）");

    public static Error Node(string flow, string node, string message) =>
        Error.Validation(ErrorCodes.WorkflowNode, $"流程 {flow} 的节点 {node}：{message}");

    public static Error Agent(string flow, string agent, string message) =>
        Error.Validation(ErrorCodes.WorkflowNode, $"流程 {flow} 的 agent {agent}：{message}");

    public static Error Body(string flow, string message) =>
        Error.Validation(ErrorCodes.WorkflowBody, $"流程 {flow}：{message}");

    /// <summary>没有该名称的流程模板。</summary>
    public static Error FlowNotFound(string name) =>
        Error.NotFound(ErrorCodes.WorkflowNotFound, $"没有名为 {name} 的流程。");
}

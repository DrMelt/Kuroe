using ErrorOr;
using Kuroe.Shared;

namespace Kuroe.Workflows.FlowAssembly;

/// <summary>流程装配域的错误构造。</summary>
static class FlowErrors
{
    /// <summary>有流程没写名字。</summary>
    public static Error MissingName() => Error.Validation(ErrorCodes.FlowName, "有流程缺少名称。");

    public static Error Node(string flow, string node, string message) =>
        Error.Validation(ErrorCodes.FlowNode, $"流程 {flow} 的节点 {node}：{message}");

    public static Error Model(string flow, string model, string message) =>
        Error.Validation(ErrorCodes.FlowNode, $"流程 {flow} 的模型选择 {model}：{message}");

    public static Error Body(string flow, string message) =>
        Error.Validation(ErrorCodes.FlowBody, $"流程 {flow}：{message}");

    /// <summary>没有该名称的流程模板。</summary>
    public static Error FlowNotFound(string name) =>
        Error.NotFound(ErrorCodes.FlowNotFound, $"没有名为 {name} 的流程。");

    /// <summary>导入的文件不能独立装配。</summary>
    public static Error ImportStandalone() =>
        Error.Validation(ErrorCodes.FlowImport, "导入的文件必须能独立装配：流程引用的节点库定义要写在同一文件里。");
}

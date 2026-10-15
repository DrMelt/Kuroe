using ErrorOr;
using Kuroe.Flow.Shared;

namespace Kuroe.Flow.StaticGraph;

/// <summary>流程装配域的错误构造。</summary>
static class FlowErrors
{
    /// <summary>有流程没写名字。</summary>
    public static Error MissingName() => Error.Validation(FlowErrorCodes.FlowName, "有流程缺少名称。");

    public static Error Model(string flow, string model, string message) =>
        Error.Validation(FlowErrorCodes.FlowNode, $"流程 {flow} 的模型选择 {model}：{message}");

    public static Error Body(string flow, string message) =>
        Error.Validation(FlowErrorCodes.FlowBody, $"流程 {flow}：{message}");

    /// <summary>没有该名称的流程模板。</summary>
    public static Error FlowNotFound(string name) =>
        Error.NotFound(FlowErrorCodes.FlowNotFound, $"没有名为 {name} 的流程。");

    /// <summary>用户层未设置默认流程。</summary>
    public static Error DefaultUnset() =>
        Error.Validation(FlowErrorCodes.FlowNotFound, "未设置默认流程。");

    /// <summary>导入的文件不能独立装配。</summary>
    public static Error ImportStandalone() =>
        Error.Validation(FlowErrorCodes.FlowImport, "导入的文件必须能独立装配：流程引用的节点要用内置节点或写在同一文件里。");
}

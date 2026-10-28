using ErrorOr;

namespace Kuroe.Flow.Shared.StaticGraph.Flow;

/// <summary>流程与流程装配的错误构造。</summary>
public static class FlowErrors
{
    /// <summary>一条流程整体的错误。</summary>
    public static Error Body(string flow, string message) =>
        Error.Validation(FlowErrorCodes.FlowBody, $"流程 {flow}：{message}");

    /// <summary>没有该名称的流程模板。</summary>
    public static Error FlowNotFound(string name) =>
        Error.NotFound(FlowErrorCodes.FlowNotFound, $"没有名为 {name} 的流程。");

    /// <summary>导入的文件不能独立装配。</summary>
    public static Error ImportStandalone() =>
        Error.Validation(FlowErrorCodes.FlowImport, "导入的文件必须能独立装配：流程引用的节点要用内置节点或写在同一文件里。");
}

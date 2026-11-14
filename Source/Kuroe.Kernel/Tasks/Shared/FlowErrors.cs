using ErrorOr;

namespace Kuroe.Tasks.Shared;

/// <summary>流程与流程装配的错误构造。</summary>
public static class FlowErrors
{
    /// <summary>一条流程整体的错误。</summary>
    public static Error Body(string flow, string message) =>
        Error.Validation(FlowErrorCodes.FlowComposition, $"流程 {flow}：{message}");

    /// <summary>没有该名称的流程模板。</summary>
    public static Error FlowNotFound(string name) =>
        Error.NotFound(FlowErrorCodes.FlowNotFound, $"没有名为 {name} 的流程。");
}
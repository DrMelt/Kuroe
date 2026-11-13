namespace Kuroe.Flow.Execution.Shared;

/// <summary>一次执行的产出形态。</summary>
public enum ExecutionOutputKind
{
    /// <summary>执行经执行端口交回产出。</summary>
    Ports,

    /// <summary>执行产出一段文本，由流程控制映射到承载内容的结果端口。</summary>
    Content,
}

/// <summary>一次执行的产出：端口值或文本内容。</summary>
public sealed record ExecutionOutput(
    ExecutionOutputKind Kind,
    IReadOnlyList<ExecutionOutputValue> Values,
    string Content = "")
{
    /// <summary>按执行端口交回产出。</summary>
    public static ExecutionOutput FromPorts(IReadOnlyList<ExecutionOutputValue> values) =>
        new(ExecutionOutputKind.Ports, values);

    /// <summary>按文本交回产出。</summary>
    public static ExecutionOutput FromContent(string content) =>
        new(ExecutionOutputKind.Content, [], content);
}
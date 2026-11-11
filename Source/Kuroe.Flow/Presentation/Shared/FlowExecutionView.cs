namespace Kuroe.Flow.Presentation;

/// <summary>展示用一次执行：标识、状态、过程与输出端口。</summary>
public sealed record FlowExecutionView(
    int Id,
    string State,
    string? Progress,
    IReadOnlyList<FlowPortView> Outputs);
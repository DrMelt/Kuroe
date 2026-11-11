namespace Kuroe.Flow.Presentation;

/// <summary>展示用步骤：路径、标签、种类、状态、输入、输出与执行列表。</summary>
public sealed record FlowStepView(
    string Path,
    string Label,
    string Kind,
    string State,
    IReadOnlyList<FlowPortView> Inputs,
    IReadOnlyList<FlowPortView> Outputs,
    IReadOnlyList<FlowExecutionView> Executions,
    int MaxExecutions);
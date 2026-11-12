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
    int MaxExecutions)
{
    /// <summary>所属分组的路径，根级步骤为空。</summary>
    public string? ParentPath { get; init; }
}
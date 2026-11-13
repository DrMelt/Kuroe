namespace Kuroe.Flow.Tasks;

/// <summary>恢复任务时推进循环尚未处理的步骤。</summary>
public sealed record FlowSeed(IReadOnlyList<FlowStepPath> Steps)
{
    /// <summary>没有待处理步骤的恢复种子。</summary>
    public static FlowSeed Empty { get; } = new([]);
}

/// <summary>等待中的步骤种类。</summary>
public enum FlowPendingKind
{
    /// <summary>等待外部输入。</summary>
    Input,

    /// <summary>等待批准。</summary>
    Approve,
}

/// <summary>一个正在等待外部输入的步骤：路径、等待种类与给外部的提问。</summary>
public sealed record FlowPendingStep(
    FlowStepPath Step,
    FlowPendingKind Kind,
    string? Question);
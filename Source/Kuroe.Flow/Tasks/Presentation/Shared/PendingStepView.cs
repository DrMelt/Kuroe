namespace Kuroe.Flow.Tasks.Presentation.Shared;

/// <summary>任务快照里等待外部输入的步骤种类。</summary>
public enum PendingStepKind
{
    /// <summary>等待外部输入。</summary>
    Input,

    /// <summary>等待批准。</summary>
    Approve,
}

/// <summary>任务快照里一个正在等待外部输入的步骤：路径、等待种类与提问。</summary>
public sealed record PendingStepView(string Path, PendingStepKind Kind, string? Question);
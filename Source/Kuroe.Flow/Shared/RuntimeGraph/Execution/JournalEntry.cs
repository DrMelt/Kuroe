namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>过程记录的严重程度。</summary>
public enum JournalLevel
{
    /// <summary>一般过程信息。</summary>
    Information,

    /// <summary>可恢复的问题或需要留意的结果。</summary>
    Warning,

    /// <summary>失败或阻塞原因。</summary>
    Error,
}

/// <summary>过程记录中的一条，按发生顺序排列。标签与级别由写入方给出，取用方按级别与文本展示，不解析标签语义。</summary>
/// <param name="At">发生时刻。</param>
/// <param name="Level">严重程度。</param>
/// <param name="Label">条目标签，由写入方给出，取用方不解析。</param>
/// <param name="Text">记录文本。首行为摘要，其余行为详情，至少一行。</param>
public sealed record JournalEntry(DateTimeOffset At, JournalLevel Level, string Label, string Text);

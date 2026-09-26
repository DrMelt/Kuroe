namespace Kuroe.Workflows.Engine;

/// <summary>宿主对流程的意图：启动或批准、返工后继续推进。</summary>
public enum FlowIntent
{
    /// <summary>提交任务，开始第一个叶子。</summary>
    Start,

    /// <summary>用户批准了等待放行的节点。</summary>
    Approved,

    /// <summary>用户要求对被阻塞的单元返工。</summary>
    Reworked,
}

/// <summary>流程控制消息：激活某叶子的某条单元，或承载宿主意图。节点推进消息由 start 或叶子执行器定向发出。</summary>
public sealed record FlowMessage
{
    /// <summary>宿主意图，节点推进消息为空。</summary>
    public FlowIntent? Intent { get; init; }

    /// <summary>要激活的叶子序号。</summary>
    public int? NodeIndex { get; init; }

    /// <summary>要激活的条目序号，整叶激活为空。</summary>
    public int? ItemIndex { get; init; }
}
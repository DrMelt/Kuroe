using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Tools.KuroeTools;

/// <summary>库与宿主共用的状态、展开方式、放行方式与时间展示名。</summary>
public static class InfoLabels
{
    /// <summary>任务状态的展示名。</summary>
    public static string Of(TaskState state) => state switch
    {
        TaskState.Running => "执行中",
        TaskState.AwaitingApproval => "待批准",
        TaskState.Blocked => "已阻塞",
        TaskState.Done => "已完成",
        TaskState.Canceled => "已取消",
        _ => state.ToString(),
    };

    /// <summary>执行节点状态的展示名。</summary>
    public static string Of(NodeState state) => state switch
    {
        NodeState.Pending => "待执行",
        NodeState.Running => "推进中",
        NodeState.AwaitingApproval => "待批准",
        NodeState.Blocked => "已阻塞",
        NodeState.Done => "已完成",
        NodeState.Canceled => "已取消",
        _ => state.ToString(),
    };

    /// <summary>执行节点展开方式的展示名。</summary>
    public static string Of(NodeMode mode) => mode switch
    {
        NodeMode.PerItem => "按条目",
        _ => "整节点",
    };

    /// <summary>执行节点产出后放行方式的展示名。</summary>
    public static string Of(NodeGate gate) => gate switch
    {
        NodeGate.Review => "需批准",
        _ => "自动放行",
    };

    /// <summary>按条目序号或整体展开的展示名。</summary>
    public static string Item(int? itemIndex) => itemIndex is { } index ? $"条目 {index + 1}" : "整体";

    /// <summary>耗时的展示名。</summary>
    public static string Elapsed(TimeSpan span) => span.TotalMinutes < 1
        ? $"{span.TotalSeconds:0}秒"
        : $"{(int)span.TotalMinutes}分{span.Seconds:00}秒";

    /// <summary>时刻的展示名，未发生时用占位。</summary>
    public static string Clock(DateTimeOffset? at) => at is null ? "—" : at.Value.ToLocalTime().ToString("HH:mm:ss");
}

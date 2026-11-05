using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;

namespace Kuroe.Flow.Shared.Display;

/// <summary>库与宿主共用的状态与时间展示名。</summary>
public static class InfoLabels
{
    /// <summary>任务状态的展示名。</summary>
    public static string Of(TaskState state) => state.Label();

    /// <summary>执行节点状态的展示名。</summary>
    public static string Of(NodeState state) => state switch
    {
        NodeState.Pending => "待执行",
        NodeState.Running => "执行中",
        NodeState.Blocked => "已阻塞",
        NodeState.Done => "已完成",
        NodeState.Canceled => "已取消",
        _ => state.ToString(),
    };

    /// <summary>执行状态的展示名，未收口时附当前活动。</summary>
    public static string State(ExecutionSnapshot execution) =>
        execution.State.IsSettled() || execution.Progress.Length == 0
            ? execution.State.Label()
            : $"{execution.State.Label()}（{execution.Progress}）";

    /// <summary>耗时的展示名。</summary>
    public static string Elapsed(TimeSpan span) => span.TotalMinutes < 1
        ? $"{span.TotalSeconds:0}秒"
        : $"{(int)span.TotalMinutes}分{span.Seconds:00}秒";

    /// <summary>执行请求次数的展示名。</summary>
    public static string Requests(ExecutionUsage usage) => $"{usage.RequestCount} 次";

    /// <summary>token 用量的展示名，未取得请求级计数时用占位。</summary>
    public static string Tokens(TokenUsage tokens) => tokens.Known
        ? $"输入 {Count(tokens.Input)} · 输出 {Count(tokens.Output)} · 合计 {Count(tokens.Total)}"
        : "未提供";

    /// <summary>最近一次请求的上下文 token 数，没有取得时用占位。</summary>
    public static string Context(ExecutionUsage usage) => usage.ContextTokens is { } tokens
        ? tokens.ToString("N0")
        : "未提供";

    /// <summary>用量的补充说明：缓存、推理与未提供用量的请求数，都没有时为空。</summary>
    public static string UsageDetails(ExecutionUsage usage)
    {
        List<string> parts = [];
        if (usage.Tokens.CachedInput is { } cached)
        {
            parts.Add($"缓存命中 {cached:N0}");
        }

        if (usage.Tokens.Reasoning is { } reasoning)
        {
            parts.Add($"推理 {reasoning:N0}");
        }

        if (usage.RequestsWithoutUsage > 0)
        {
            parts.Add($"未提供用量 {usage.RequestsWithoutUsage} 次");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>时刻的展示名，未发生时用占位。</summary>
    public static string Clock(DateTimeOffset? at) => at is null ? "—" : at.Value.ToLocalTime().ToString("HH:mm:ss");

    private static string Count(long? value) => value?.ToString("N0") ?? "—";
}

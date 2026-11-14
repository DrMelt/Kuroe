using ExecutionUsage = Kuroe.Execution.Shared.ExecutionUsage;
using TokenUsage = Kuroe.Execution.Shared.TokenUsage;
using Kuroe.Tasks.Shared;

namespace Kuroe.Presentation.Shared;

/// <summary>库与宿主共用的状态与时间展示名。</summary>
public static class InfoLabels
{
    /// <summary>任务状态的展示名。</summary>
    public static string Of(TaskState state) => state.Label();

    /// <summary>耗时的展示名。</summary>
    public static string Elapsed(TimeSpan span) => span.TotalMinutes < 1
        ? $"{span.TotalSeconds:0}秒"
        : $"{(int)span.TotalMinutes}分{span.Seconds:00}秒";

    /// <summary>执行请求次数的展示名，未返回用量的请求数附在次数后。</summary>
    public static string Requests(ExecutionUsage usage) => usage.RequestsWithoutUsage switch
    {
        0 => $"{usage.RequestCount} 次",
        var missing when missing == usage.RequestCount => $"{usage.RequestCount} 次（均未返回用量）",
        var missing => $"{usage.RequestCount} 次（{missing} 次未返回用量）",
    };

    /// <summary>token 用量的展示条目：输入、输出与合计。缓存输入并入输入，推理并入输出。未取得请求级计数时只给一个未提供条目。</summary>
    public static IReadOnlyList<(string Label, string Value)> TokenEntries(ExecutionUsage usage)
    {
        TokenUsage tokens = usage.Tokens;
        if (!tokens.Known)
        {
            return [("用量", "未提供")];
        }

        long? inputShare = InputShare(tokens);
        string input = $"{Count(tokens.Input)}{Percent(inputShare)}";
        if (tokens.CachedInput is { } cached)
        {
            input = $"{input}，缓存输入 {cached:N0}{ShareOf(cached, tokens.Input, "输入")}";
        }

        string output = $"{Count(tokens.Output)}{Percent(inputShare is { } share ? 100 - share : (long?)null)}";
        if (tokens.Reasoning is { } reasoning)
        {
            output = $"{output}，推理 {reasoning:N0}{ShareOf(reasoning, tokens.Output, "输出")}";
        }

        return
        [
            ("输入", input),
            ("输出", output),
            ("合计", Count(tokens.Total)),
        ];
    }

    /// <summary>最近一次请求的上下文 token 数，没有取得时用占位。</summary>
    public static string Context(ExecutionUsage usage) => usage.ContextTokens is { } tokens
        ? tokens.ToString("N0")
        : "未提供";

    /// <summary>时刻的展示名，未发生时用占位。</summary>
    public static string Clock(DateTimeOffset? at) => at is null ? "—" : at.Value.ToLocalTime().ToString("HH:mm:ss");

    /// <summary>输入在输入与输出之和中的占比，两者任一未知或基数为 0 时为空。</summary>
    private static long? InputShare(TokenUsage tokens) =>
        tokens.Input is { } input && tokens.Output is { } output && input + output > 0
            ? (long)Math.Round(input * 100d / (input + output), MidpointRounding.AwayFromZero)
            : null;

    /// <summary>子项占母类的比例，母类未知、为 0 或子项超出母类时为空。</summary>
    private static string ShareOf(long part, long? whole, string name) =>
        whole is { } total && total > 0 && part >= 0 && part <= total
            ? $"（占{name} {(long)Math.Round(part * 100d / total, MidpointRounding.AwayFromZero)}%）"
            : string.Empty;

    private static string Percent(long? share) => share is { } value ? $"（{value}%）" : string.Empty;

    private static string Count(long? value) => value?.ToString("N0") ?? "—";
}
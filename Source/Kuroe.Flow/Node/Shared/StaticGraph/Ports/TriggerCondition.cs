namespace Kuroe.Flow.Node.Shared.StaticGraph.Ports;

/// <summary>触发门控条件：来源触发值满足条件时启动组放行。</summary>
public enum TriggerCondition
{
    /// <summary>触发值为 true 时放行。</summary>
    Positive,

    /// <summary>触发值为 false 时放行，即翻转门控。</summary>
    Negative,
}

/// <summary>触发门控条件的判定。</summary>
public static class TriggerConditions
{
    /// <summary>来源触发值是否满足条件。</summary>
    public static bool Accepts(this TriggerCondition condition, bool fires) =>
        condition == TriggerCondition.Negative ? !fires : fires;
}
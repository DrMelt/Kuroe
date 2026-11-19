namespace Kuroe.Document.Shared;

/// <summary>触发门控条件的描述取值：来源触发值满足条件时启动组放行。未写条件时按正向。</summary>
public enum TriggerConditionDocument
{
    /// <summary>触发值为 true 时放行。</summary>
    Positive,

    /// <summary>触发值为 false 时放行，即翻转门控。</summary>
    Negative,
}
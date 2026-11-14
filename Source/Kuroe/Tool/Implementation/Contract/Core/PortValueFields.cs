namespace Kuroe.Tool.Contract;

/// <summary>端口交回对象的字段名与说明。schema 与解析共用，字段名按原文匹配。</summary>
internal static class PortValueFields
{
    /// <summary>文本字段。</summary>
    public const string Text = "text";

    /// <summary>触发位字段。</summary>
    public const string Trigger = "trigger";

    /// <summary>文本字段的说明。</summary>
    public const string TextDescription = "交回的文本";

    /// <summary>触发位字段的说明。</summary>
    public const string TriggerDescription = "false 表示本次产出只被下游启动组消耗、不触发下游节点";
}
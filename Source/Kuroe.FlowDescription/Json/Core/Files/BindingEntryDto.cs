namespace Kuroe.FlowDescription.Json.Files;

/// <summary>带触发条件的 From 连接形状：Ref 写引用，Condition 写 Positive 或 Negative。写出时未写条件或条件为 Positive 仍写成引用文本。</summary>
public sealed class BindingEntryDto
{
    /// <summary>来源引用，写 @端口 或 来源@端口。</summary>
    public string? Ref { get; set; }

    /// <summary>触发门控条件，取值 Positive 或 Negative。</summary>
    public string? Condition { get; set; }
}
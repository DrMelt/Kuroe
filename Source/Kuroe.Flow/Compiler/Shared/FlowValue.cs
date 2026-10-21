namespace Kuroe.Flow.Compiler.Shared;

/// <summary>描述层的中立值：承载参数段与原始键值，不依赖承载格式。</summary>
public abstract record FlowValue
{
    /// <summary>文本值。</summary>
    public sealed record Text(string Value) : FlowValue;

    /// <summary>数字值，保留原文。</summary>
    public sealed record Number(string Raw) : FlowValue;

    /// <summary>布尔值。</summary>
    public sealed record Boolean(bool Value) : FlowValue;

    /// <summary>空值。</summary>
    public sealed record Null : FlowValue;

    /// <summary>数组值。</summary>
    public sealed record Array(IReadOnlyList<FlowValue> Items) : FlowValue;

    /// <summary>对象值。</summary>
    public sealed record Object(IReadOnlyDictionary<string, FlowValue> Members) : FlowValue;
}
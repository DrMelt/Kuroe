namespace Kuroe.Flow.Shared.Descriptions;

/// <summary>参数段取值的形态：一个字段承载的值种类。</summary>
public enum ParameterValueKind
{
    /// <summary>文本值。</summary>
    Text,

    /// <summary>文本数组。</summary>
    Texts,

    /// <summary>整数值。</summary>
    Integer,

    /// <summary>文本到文本的映射对象。</summary>
    Map,

    /// <summary>文本到文本映射的映射对象。</summary>
    MapMap,
}
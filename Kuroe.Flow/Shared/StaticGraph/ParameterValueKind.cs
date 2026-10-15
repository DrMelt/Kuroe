namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>参数段取值的形态：文件里一个字段承载的 JSON 形状。</summary>
public enum ParameterValueKind
{
    /// <summary>文本值。</summary>
    Text,

    /// <summary>文本数组。</summary>
    Texts,
}
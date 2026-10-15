namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>参数段取值的读侧：按字段名取本轮取值，形状不符经 fail 报出，缺失取安全默认。
/// 由文件层的扩展实现，类型只按名字取用，不认识承载格式。</summary>
public interface IParameterReader
{
    /// <summary>取文本值，未写或为空值时为 null。</summary>
    string? Text(string name);

    /// <summary>取文本数组，未写时为空集合。</summary>
    IReadOnlyList<string> Texts(string name);
}
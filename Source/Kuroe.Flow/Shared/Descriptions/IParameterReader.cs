namespace Kuroe.Flow.Shared.Descriptions;

/// <summary>参数段取值的读侧：按字段名取本轮取值，形状不符经 fail 报出，缺失取安全默认。</summary>
public interface IParameterReader
{
    /// <summary>取文本值，未写或为空值时为 null。</summary>
    string? Text(string name);

    /// <summary>取文本数组，未写时为空集合。</summary>
    IReadOnlyList<string> Texts(string name);
}
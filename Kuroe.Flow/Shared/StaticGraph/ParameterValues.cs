namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>参数段的一轮取值：按字段名置入的取值集合，文件层按形态写回 JSON。不含 JSON 类型，形态与内容在此声明。</summary>
public sealed class ParameterValues
{
    private readonly List<ParameterValue> _values = [];

    /// <summary>已置入的取值，按置入序。</summary>
    public IReadOnlyList<ParameterValue> Values => _values;

    /// <summary>置入一个文本值。</summary>
    public void Set(string name, string value) => _values.Add(new ParameterValue
    {
        Name = name,
        Kind = ParameterValueKind.Text,
        Text = value,
    });

    /// <summary>置入一个文本数组。</summary>
    public void Set(string name, IReadOnlyList<string> values) => _values.Add(new ParameterValue
    {
        Name = name,
        Kind = ParameterValueKind.Texts,
        Texts = values,
    });
}
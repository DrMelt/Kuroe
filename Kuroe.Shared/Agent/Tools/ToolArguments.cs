using System.Globalization;
using System.Text.Json;

namespace Kuroe.Shared.Agent.Tools;

/// <summary>一次工具调用的实参。值来自模型给的 JSON，也接受已经解析成 CLR 值的同义形式。</summary>
public sealed class ToolArguments(IReadOnlyDictionary<string, object?> values)
{
    /// <summary>按参数名取文本。标量按文本给出，缺参、JSON null 或类型不符时为空。</summary>
    public string? Text(string name) => Value(name) switch
    {
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
        JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
        JsonElement element => element.ToString(),
        bool flag => flag ? "true" : "false",
        string text => text,
        float number => number.ToString(CultureInfo.InvariantCulture),
        double number => number.ToString(CultureInfo.InvariantCulture),
        null => null,
        object other => other.ToString(),
    };

    /// <summary>按参数名取真假值。文本形式只认 true 与 false，取不到时为空。</summary>
    public bool? Flag(string name) => Value(name) switch
    {
        JsonElement { ValueKind: JsonValueKind.True } => true,
        JsonElement { ValueKind: JsonValueKind.False } => false,
        JsonElement { ValueKind: JsonValueKind.String } element when bool.TryParse(element.GetString(), out bool parsed) => parsed,
        bool flag => flag,
        string text when bool.TryParse(text, out bool parsed) => parsed,
        _ => null,
    };

    private object? Value(string name) => values.TryGetValue(name, out object? value) ? value : null;
}

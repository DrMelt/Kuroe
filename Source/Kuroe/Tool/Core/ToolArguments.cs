using System.Globalization;
using System.Text.Json;

namespace Kuroe.Tool;

/// <summary>一次工具调用的实参。值来自模型给的 JSON，也接受已经解析成 CLR 值的同义形式。</summary>
public sealed class ToolArguments(IReadOnlyDictionary<string, object?> values)
{
    /// <summary>按参数名取文本。标量按文本给出，缺参、JSON null 或类型不符时为空。</summary>
    public string? Text(ToolName name) => Value(name) switch
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

    /// <summary>按参数名取必填文本。必填路径参数在调用前已由 <see cref="ToolFunction"/> 校验，取不到说明声明与调用体不一致。</summary>
    public string RequiredText(ToolName name) =>
        Text(name) ?? throw new InvalidOperationException($"必填参数 {name.Value} 缺失。");

    /// <summary>按参数名取真假值。文本形式只认 true 与 false，取不到时为空。</summary>
    public bool? Flag(ToolName name) => Value(name) switch
    {
        JsonElement { ValueKind: JsonValueKind.True } => true,
        JsonElement { ValueKind: JsonValueKind.False } => false,
        JsonElement { ValueKind: JsonValueKind.String } element when bool.TryParse(element.GetString(), out bool parsed) => parsed,
        bool flag => flag,
        string text when bool.TryParse(text, out bool parsed) => parsed,
        _ => null,
    };

    /// <summary>按参数名取字符串列表。单个字符串按单元素列表给出，数组元素按文本给出，非字符串元素按文本表示；缺参、JSON null 或类型不符时为空。</summary>
    public IReadOnlyList<string>? List(ToolName name) => Value(name) switch
    {
        JsonElement { ValueKind: JsonValueKind.Array } element =>
            [.. element.EnumerateArray().Select(TextOf)],
        JsonElement { ValueKind: JsonValueKind.String } element => [element.GetString() ?? string.Empty],
        string text => [text],
        string[] array => [.. array],
        IReadOnlyList<string> list => [.. list],
        _ => null,
    };

    /// <summary>数组元素按文本给出，JSON null 或 undefined 按空文本。</summary>
    private static string TextOf(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => element.ToString(),
    };

    private object? Value(ToolName name) => values.TryGetValue(name.Value, out object? value) ? value : null;
}

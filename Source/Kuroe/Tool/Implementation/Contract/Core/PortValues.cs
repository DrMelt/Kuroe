using System.Text.Json;
using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Tool.Contract;

/// <summary>输出端口交回的命名段：把模型给的 JSON 对象按端口声明解析成端口值，不合法时给出一条可回给模型的原因。
/// 顶层键按原文精确匹配声明端口，区分大小写；端口对象内的字段也按原文精确匹配。触发端口的触发位必须写出。</summary>
internal static class PortValues
{
    /// <summary>解析端口名到端口值的 JSON 对象，取值形态按端口声明判定。</summary>
    public static ErrorOr<IReadOnlyDictionary<PortName, PortValue>> Parse(string valuesJson, IReadOnlyList<Port>? declared)
    {
        if (declared is not { Count: > 0 })
        {
            return [PortError("没有可交回的端口声明")];
        }

        Dictionary<string, JsonElement>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize(valuesJson, PortJson.Default.DictionaryStringJsonElement);
        }
        catch (JsonException ex)
        {
            return [PortError($"不是合法 JSON：{ex.Message}")];
        }

        if (parsed is null || parsed.Count == 0)
        {
            return [PortError("端口产出为空")];
        }

        Dictionary<PortName, PortValue> result = [];
        foreach ((string key, JsonElement element) in parsed)
        {
            ErrorOr<PortName> name = PortName.Create(key);
            if (name.IsError)
            {
                return name.ErrorsOrEmptyList;
            }

            if (declared.Find(name.Value) is not { } port)
            {
                return [PortError($"不认识端口 {name.Value}，应提交 {PortRules.Names(declared)}")];
            }

            if (result.ContainsKey(name.Value))
            {
                return [PortError($"端口 {name.Value} 重复提交")];
            }

            ErrorOr<PortValue> value = Read(port, element);
            if (value.IsError)
            {
                return value.ErrorsOrEmptyList;
            }

            result[name.Value] = value.Value;
        }

        return result;
    }

    /// <summary>按端口交回形态读取一个取值。</summary>
    private static ErrorOr<PortValue> Read(Port port, JsonElement element)
    {
        PortSubmissionShape shape = port.SubmissionShape();

        return shape.IsObject() ? ReadObject(port, shape, element) : ReadText(port, shape, element);
    }

    /// <summary>普通端口取值：只接受文本。承载内容的端口保留文本，不承载内容的端口丢弃文本。</summary>
    private static ErrorOr<PortValue> ReadText(Port port, PortSubmissionShape shape, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            return [ValueError(port, shape.IncludesText() ? "普通端口只接受文本" : "普通端口只接受空文本或端口名")];
        }

        return new PortValue(shape.IncludesText() ? element.GetString() : null);
    }

    /// <summary>触发端口取值：只接受对象，字段按形态给出，字段名与未知键都按原文匹配。</summary>
    private static ErrorOr<PortValue> ReadObject(Port port, PortSubmissionShape shape, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return [ValueError(port, "取值必须是对象")];
        }

        string? text = null;
        bool? trigger = null;
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (shape.IncludesText() && string.Equals(property.Name, PortValueFields.Text, StringComparison.Ordinal))
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    return [ValueError(port, $"{PortValueFields.Text} 必须是文本")];
                }

                text = property.Value.GetString();
                continue;
            }

            if (shape.IncludesTrigger() && string.Equals(property.Name, PortValueFields.Trigger, StringComparison.Ordinal))
            {
                if (property.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    return [ValueError(port, $"{PortValueFields.Trigger} 必须是 true 或 false")];
                }

                trigger = property.Value.GetBoolean();
                continue;
            }

            return [ValueError(port, $"不认识键 {property.Name}，对象只写 {Fields(shape)}")];
        }

        if (shape.IncludesText() && text is null)
        {
            return [ValueError(port, $"对象必须写 {PortValueFields.Text}")];
        }

        if (shape.IncludesTrigger() && trigger is null)
        {
            return [ValueError(port, $"对象必须写 {PortValueFields.Trigger}")];
        }

        return new PortValue(text, trigger);
    }

    /// <summary>触发对象允许的字段清单。</summary>
    private static string Fields(PortSubmissionShape shape) =>
        shape.IncludesText()
            ? $"{PortValueFields.Text} 与 {PortValueFields.Trigger}"
            : PortValueFields.Trigger;

    private static Error PortError(string reason) => Error.Validation("Tool.Argument", $"{reason}。");

    private static Error ValueError(Port port, string reason) => Error.Validation(
        "Tool.Argument", $"端口 {port.Name} {reason}。");
}
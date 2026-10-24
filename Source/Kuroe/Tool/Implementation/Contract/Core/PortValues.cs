using System.Text.Json;
using ErrorOr;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Tool.Contract;

/// <summary>输出端口交回的命名段：把模型给的 JSON 对象按端口声明解析成端口值，不合法时给出一条可回给模型的原因。
/// Text 与 Signal 交回原有形态；TextTrigger 接受文本或带 text 与 trigger 的对象，SignalTrigger 接受布尔值或带 trigger 的对象。</summary>
internal static class PortValues
{
    /// <summary>解析端口名到端口值的 JSON 对象，取值形态按声明类型判定。</summary>
    public static ErrorOr<IReadOnlyDictionary<string, PortValue>> Parse(string valuesJson, IReadOnlyList<Port>? declared)
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

        Dictionary<string, PortValue> result = [];
        foreach ((string name, JsonElement element) in parsed)
        {
            if (declared.Find(new PortName(name)) is not { } port)
            {
                result[name] = new PortValue(element.ValueKind == JsonValueKind.String ? element.GetString() : null);

                continue;
            }

            ErrorOr<PortValue> value = Read(port, element);
            if (value.IsError)
            {
                return value.ErrorsOrEmptyList;
            }

            result[name] = value.Value;
        }

        return result;
    }

    /// <summary>按端口类型读取一个取值。普通端口只接受原有形态；TextTrigger 接受文本或带 text 与 trigger 的对象，SignalTrigger 接受布尔值或带 trigger 的对象。</summary>
    private static ErrorOr<PortValue> Read(Port port, JsonElement element)
    {
        if (!port.Type.IsTrigger)
        {
            if (element.ValueKind is JsonValueKind.String or JsonValueKind.Null)
            {
                return new PortValue(element.ValueKind == JsonValueKind.String ? element.GetString() : null);
            }

            string allowed = port.Type.Semantics == PortSemantics.Text ? "取值为文本" : "取值为空文本或端口名";

            return [ValueError(port, $"普通端口不承载触发值，{allowed}")];
        }

        if (port.Type.Semantics == PortSemantics.Text)
        {
            return ReadTextTrigger(port, element);
        }

        return ReadSignalTrigger(port, element);
    }

    /// <summary>TextTrigger 取值：字符串即触发，对象同时给 text 与 trigger。</summary>
    private static ErrorOr<PortValue> ReadTextTrigger(Port port, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return new PortValue(element.GetString());
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return [ValueError(port, "取值为文本，或带 text 与 trigger 的对象")];
        }

        string? text = null;
        bool trigger = true;
        foreach (JsonProperty property in element.EnumerateObject())
        {
            switch (property.Name.ToLowerInvariant())
            {
                case "text" when property.Value.ValueKind == JsonValueKind.String:
                    text = property.Value.GetString();
                    break;

                case "text":
                    return [ValueError(port, "text 必须是文本")];

                case "trigger" when property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False:
                    trigger = property.Value.GetBoolean();
                    break;

                case "trigger":
                    return [ValueError(port, "trigger 必须是 true 或 false")];

                default:
                    return [ValueError(port, $"不认识键 {property.Name}，对象只写 text 与 trigger")];
            }
        }

        return new PortValue(text, trigger);
    }

    /// <summary>SignalTrigger 取值：布尔值即触发值，对象写 trigger，空值按 true。</summary>
    private static ErrorOr<PortValue> ReadSignalTrigger(Port port, JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return new PortValue(null, element.GetBoolean());
        }

        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.String)
        {
            return new PortValue(null);
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return [ValueError(port, "取值为 true 或 false，或带 trigger 的对象")];
        }

        bool trigger = true;
        foreach (JsonProperty property in element.EnumerateObject())
        {
            switch (property.Name.ToLowerInvariant())
            {
                case "trigger" when property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False:
                    trigger = property.Value.GetBoolean();
                    break;

                case "trigger":
                    return [ValueError(port, "trigger 必须是 true 或 false")];

                default:
                    return [ValueError(port, $"不认识键 {property.Name}，对象只写 trigger")];
            }
        }

        return new PortValue(null, trigger);
    }

    private static Error PortError(string reason) => Error.Validation(
        "Tool.Argument", $"{reason}。要提交端口名到值的 JSON 对象。");

    private static Error ValueError(Port port, string reason) => Error.Validation(
        "Tool.Argument", $"端口 {port.Name} {reason}。");
}
using System.Text.Json;
using ErrorOr;
using Kuroe.Flow.Execution;

namespace Kuroe.Tool.Contract;

/// <summary>输出端口交回的命名段：把模型给的 JSON 对象按端口声明解析成端口值，不合法时给出一条可回给模型的原因。
/// 不承载触发的端口只接受内容形态；承载触发的 Text 端口接受文本或带 text 与 trigger 的对象，
/// 承载触发的 Signal 端口接受布尔值或带 trigger 的对象。</summary>
internal static class PortValues
{
    /// <summary>解析端口名到端口值的 JSON 对象，取值形态按端口配置判定。</summary>
    public static ErrorOr<IReadOnlyList<ExecutionOutputValue>> Parse(
        string valuesJson,
        IReadOnlyList<ExecutionOutputPort>? declared)
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

        List<ExecutionOutputValue> result = [];
        foreach ((string name, JsonElement element) in parsed)
        {
            ExecutionPortName portName = new(name);
            if (declared.Find(portName) is not { } port)
            {
                result.Add(new ExecutionOutputValue(
                    portName,
                    element.ValueKind == JsonValueKind.String ? element.GetString() : null));

                continue;
            }

            ErrorOr<ExecutionOutputValue> value = Read(port, element);
            if (value.IsError)
            {
                return value.ErrorsOrEmptyList;
            }

            result.Add(value.Value);
        }

        return result;
    }

    /// <summary>按端口配置读取一个取值。不承载触发的端口只接受内容形态，Signal 不承载内容；承载触发的端口按数据语义读取。</summary>
    private static ErrorOr<ExecutionOutputValue> Read(ExecutionOutputPort port, JsonElement element)
    {
        if (!port.CarriesTrigger)
        {
            if (element.ValueKind is JsonValueKind.String or JsonValueKind.Null)
            {
                if (port.Kind == ExecutionPortKind.Signal)
                {
                    return new ExecutionOutputValue(port.Name, null);
                }

                return new ExecutionOutputValue(
                    port.Name,
                    element.ValueKind == JsonValueKind.String ? element.GetString() : null);
            }

            string allowed = port.Kind == ExecutionPortKind.Text ? "取值为文本" : "取值为空文本或端口名";

            return [ValueError(port, $"普通端口不承载触发值，{allowed}")];
        }

        if (port.Kind == ExecutionPortKind.Text)
        {
            return ReadTriggeredText(port, element);
        }

        return ReadTriggeredSignal(port, element);
    }

    /// <summary>Text 触发端口取值：字符串即触发，对象同时给 text 与 trigger。</summary>
    private static ErrorOr<ExecutionOutputValue> ReadTriggeredText(ExecutionOutputPort port, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return new ExecutionOutputValue(port.Name, element.GetString());
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

        return new ExecutionOutputValue(port.Name, text, trigger);
    }

    /// <summary>Signal 触发端口取值：布尔值即触发值，对象写 trigger，空值按 true。</summary>
    private static ErrorOr<ExecutionOutputValue> ReadTriggeredSignal(ExecutionOutputPort port, JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return new ExecutionOutputValue(port.Name, null, element.GetBoolean());
        }

        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.String)
        {
            return new ExecutionOutputValue(port.Name, null);
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

        return new ExecutionOutputValue(port.Name, null, trigger);
    }

    private static Error PortError(string reason) => Error.Validation(
        "Tool.Argument", $"{reason}。要提交端口名到值的 JSON 对象。");

    private static Error ValueError(ExecutionOutputPort port, string reason) => Error.Validation(
        "Tool.Argument", $"端口 {port.Name} {reason}。");
}
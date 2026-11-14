using System.Text.Json;
using ErrorOr;
using Kuroe.Execution.Shared;

namespace Kuroe.Tool.Contract;

/// <summary>输出端口交回的命名段：把模型给的 JSON 对象按端口声明解析成端口值，不合法时给出一条可回给模型的原因。
/// 顶层键按原文精确匹配声明端口，区分大小写；端口对象内的字段也按原文精确匹配。触发端口的触发位必须写出。</summary>
internal static class PortValues
{
    /// <summary>解析端口名到端口值的 JSON 对象，取值形态按端口声明判定。</summary>
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
        HashSet<ExecutionPortName> seen = [];
        foreach ((string key, JsonElement element) in parsed)
        {
            ErrorOr<ExecutionPortName> name = ExecutionPortName.Create(key);
            if (name.IsError)
            {
                return name.ErrorsOrEmptyList;
            }

            if (declared.Find(name.Value) is not { } port)
            {
                return [PortError($"不认识端口 {name.Value}，应提交 {Names(declared)}")];
            }

            if (!seen.Add(name.Value))
            {
                return [PortError($"端口 {name.Value} 重复提交")];
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

    /// <summary>按端口形态读取一个取值。触发端口只接受对象，普通端口只接受文本。</summary>
    private static ErrorOr<ExecutionOutputValue> Read(ExecutionOutputPort port, JsonElement element) =>
        port.CarriesTrigger ? ReadObject(port, element) : ReadText(port, element);

    /// <summary>普通端口取值：只接受文本。承载内容的端口保留文本，不承载内容的端口丢弃文本。</summary>
    private static ErrorOr<ExecutionOutputValue> ReadText(ExecutionOutputPort port, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            return [ValueError(port, CarriesText(port) ? "普通端口只接受文本" : "普通端口只接受空文本或端口名")];
        }

        return new ExecutionOutputValue(port.Name, CarriesText(port) ? element.GetString() : null);
    }

    /// <summary>触发端口取值：只接受对象，字段按形态给出，字段名与未知键都按原文匹配。</summary>
    private static ErrorOr<ExecutionOutputValue> ReadObject(ExecutionOutputPort port, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return [ValueError(port, "取值必须是对象")];
        }

        bool carriesText = CarriesText(port);
        string? text = null;
        bool? trigger = null;
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (carriesText && string.Equals(property.Name, PortValueFields.Text, StringComparison.Ordinal))
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    return [ValueError(port, $"{PortValueFields.Text} 必须是文本")];
                }

                text = property.Value.GetString();
                continue;
            }

            if (string.Equals(property.Name, PortValueFields.Trigger, StringComparison.Ordinal))
            {
                if (property.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    return [ValueError(port, $"{PortValueFields.Trigger} 必须是 true 或 false")];
                }

                trigger = property.Value.GetBoolean();
                continue;
            }

            return [ValueError(port, $"不认识键 {property.Name}，对象只写 {Fields(port)}")];
        }

        if (carriesText && text is null)
        {
            return [ValueError(port, $"对象必须写 {PortValueFields.Text}")];
        }

        if (trigger is null)
        {
            return [ValueError(port, $"对象必须写 {PortValueFields.Trigger}")];
        }

        return new ExecutionOutputValue(port.Name, text, trigger.Value);
    }

    /// <summary>端口是否承载文本内容，Signal 不承载内容。</summary>
    private static bool CarriesText(ExecutionOutputPort port) => port.Kind != ExecutionPortKind.Signal;

    /// <summary>触发对象允许的字段清单。</summary>
    private static string Fields(ExecutionOutputPort port) =>
        CarriesText(port)
            ? $"{PortValueFields.Text} 与 {PortValueFields.Trigger}"
            : PortValueFields.Trigger;

    /// <summary>声明端口的原始名清单，按声明序。</summary>
    private static string Names(IReadOnlyList<ExecutionOutputPort> ports) =>
        string.Join('、', ports.Select(port => port.Name.Value));

    private static Error PortError(string reason) => Error.Validation("Tool.Argument", $"{reason}。");

    private static Error ValueError(ExecutionOutputPort port, string reason) => Error.Validation(
        "Tool.Argument", $"端口 {port.Name} {reason}。");
}
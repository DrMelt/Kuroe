using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kuroe.Flow.Execution;
using Microsoft.Extensions.AI;

namespace Kuroe.Node.Llm.Sessions;

/// <summary>一次对话的恢复数据：模型与完整历史。</summary>
internal sealed record LlmExecutionState(
    string Model,
    IReadOnlyList<LlmMessageState> Messages) : IExecutionRestoreData;

/// <summary>恢复数据中的一条消息。</summary>
internal sealed record LlmMessageState(
    string Role,
    IReadOnlyList<LlmContentState> Contents);

/// <summary>恢复数据中的一段内容，按种类使用对应字段。</summary>
internal sealed record LlmContentState(
    string Kind,
    string? Text = null,
    string? ProtectedData = null,
    string? CallId = null,
    string? Name = null,
    string? Arguments = null,
    string? Result = null);

/// <summary>会话恢复数据的编解码。工具参数保存为 JSON 文本，恢复为普通值供工具绑定使用。</summary>
internal static class LlmExecutionStateCodec
{
    /// <summary>当前恢复数据的格式名。</summary>
    internal const string Format = "llm.session.v2";

    private const string TextKind = "text";
    private const string ReasoningKind = "reasoning";
    private const string CallKind = "call";
    private const string ResultKind = "result";

    /// <summary>把恢复数据写成执行记录中的恢复数据。</summary>
    internal static ExecutionRestoreState Encode(LlmExecutionState state) =>
        new(Format, JsonSerializer.Serialize(state, LlmExecutionStateJson.Default.LlmExecutionState));

    /// <summary>从执行记录中的恢复负载读回。</summary>
    internal static LlmExecutionState Decode(ExecutionRestoreState data)
    {
        if (data.Format != Format)
        {
            throw new FormatException($"不支持的会话恢复数据格式 {data.Format}。");
        }

        return JsonSerializer.Deserialize(data.Payload, LlmExecutionStateJson.Default.LlmExecutionState)
            ?? throw new FormatException("会话恢复数据为空。");
    }

    /// <summary>历史末尾与工具调用配对是否允许从下一次请求恢复。</summary>
    internal static bool IsRestorable(LlmExecutionState state)
    {
        if (state.Messages.Count == 0)
        {
            return false;
        }

        HashSet<string> pendingCalls = [];
        foreach (LlmMessageState message in state.Messages)
        {
            foreach (LlmContentState content in message.Contents)
            {
                switch (content.Kind)
                {
                    case CallKind when content.CallId is { Length: > 0 } call:
                        pendingCalls.Add(call);
                        break;

                    case ResultKind when content.CallId is { Length: > 0 } call:
                        pendingCalls.Remove(call);
                        break;

                    case TextKind or ReasoningKind:
                        break;

                    default:
                        return false;
                }
            }
        }

        if (pendingCalls.Count > 0)
        {
            return false;
        }

        LlmMessageState last = state.Messages[^1];
        if (last.Role is "user" or "tool")
        {
            return true;
        }

        return last.Role == "assistant"
            && last.Contents.All(content => content.Kind != CallKind)
            && last.Contents.Any(content => content.Kind is TextKind or ReasoningKind);
    }

    /// <summary>把一条会话消息写成恢复数据，只保留可回放的内容。</summary>
    internal static LlmMessageState ToState(ChatMessage message)
    {
        List<LlmContentState> contents = [];
        foreach (AIContent content in message.Contents)
        {
            switch (content)
            {
                case TextContent { Text.Length: > 0 } text:
                    contents.Add(new LlmContentState(TextKind, Text: text.Text));
                    break;

                case TextReasoningContent { Text.Length: > 0 } reasoning:
                    contents.Add(new LlmContentState(
                        ReasoningKind,
                        Text: reasoning.Text,
                        ProtectedData: reasoning.ProtectedData));
                    break;

                case FunctionCallContent call:
                    contents.Add(new LlmContentState(
                        CallKind,
                        CallId: call.CallId,
                        Name: call.Name,
                        Arguments: EncodeArguments(call.Arguments)));
                    break;

                case FunctionResultContent result:
                    contents.Add(new LlmContentState(
                        ResultKind,
                        CallId: result.CallId,
                        Result: result.Result as string ?? result.Result?.ToString() ?? string.Empty));
                    break;
            }
        }

        return new LlmMessageState(message.Role.Value, contents);
    }

    /// <summary>把恢复数据中的一条消息恢复为会话消息。</summary>
    internal static ChatMessage ToMessage(LlmMessageState message)
    {
        List<AIContent> contents = [];
        foreach (LlmContentState content in message.Contents)
        {
            contents.Add(content.Kind switch
            {
                TextKind => new TextContent(content.Text ?? string.Empty),
                ReasoningKind => new TextReasoningContent(content.Text ?? string.Empty)
                {
                    ProtectedData = content.ProtectedData,
                },
                CallKind => new FunctionCallContent(
                    content.CallId ?? throw new FormatException("工具调用缺少调用号。"),
                    content.Name ?? throw new FormatException("工具调用缺少函数名。"),
                    DecodeArguments(content.Arguments)),
                ResultKind => new FunctionResultContent(
                    content.CallId ?? throw new FormatException("工具结果缺少调用号。"),
                    content.Result ?? string.Empty),
                _ => throw new FormatException($"不认识的会话内容种类 {content.Kind}。"),
            });
        }

        return new ChatMessage(new ChatRole(message.Role), contents);
    }

    /// <summary>工具参数写成 JSON 文本，空参数给空。</summary>
    private static string? EncodeArguments(IDictionary<string, object?>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
        {
            return null;
        }

        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            WriteObject(writer, arguments);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>工具参数从 JSON 文本恢复为普通值。</summary>
    private static Dictionary<string, object?>? DecodeArguments(string? arguments)
    {
        if (string.IsNullOrEmpty(arguments))
        {
            return null;
        }

        using JsonDocument document = JsonDocument.Parse(arguments);

        return document.RootElement.ValueKind == JsonValueKind.Object
            ? document.RootElement.EnumerateObject().ToDictionary(pair => pair.Name, pair => ToValue(pair.Value))
            : null;
    }

    private static void WriteObject(Utf8JsonWriter writer, IDictionary<string, object?> values)
    {
        writer.WriteStartObject();
        foreach ((string key, object? value) in values)
        {
            writer.WritePropertyName(key);
            WriteValue(writer, value);
        }

        writer.WriteEndObject();
    }

    private static object ToNumber(JsonElement element)
    {
        if (element.TryGetInt32(out int count))
        {
            return count;
        }

        return element.TryGetInt64(out long integer) ? integer : element.GetDouble();
    }

    private static object? ToValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => ToNumber(element),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(pair => pair.Name, pair => ToValue(pair.Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(ToValue).ToList(),
        _ => element.Clone(),
    };

    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;

            case string text:
                writer.WriteStringValue(text);
                break;

            case bool flag:
                writer.WriteBooleanValue(flag);
                break;

            case byte number:
                writer.WriteNumberValue(number);
                break;

            case sbyte number:
                writer.WriteNumberValue(number);
                break;

            case short number:
                writer.WriteNumberValue(number);
                break;

            case ushort number:
                writer.WriteNumberValue(number);
                break;

            case int number:
                writer.WriteNumberValue(number);
                break;

            case uint number:
                writer.WriteNumberValue(number);
                break;

            case long number:
                writer.WriteNumberValue(number);
                break;

            case ulong number:
                writer.WriteNumberValue(number);
                break;

            case float number:
                writer.WriteNumberValue(number);
                break;

            case double number:
                writer.WriteNumberValue(number);
                break;

            case decimal number:
                writer.WriteNumberValue(number);
                break;

            case JsonElement element:
                element.WriteTo(writer);
                break;

            case IDictionary<string, object?> map:
                WriteObject(writer, map);
                break;

            case IEnumerable<object?> list:
                writer.WriteStartArray();
                foreach (object? item in list)
                {
                    WriteValue(writer, item);
                }

                writer.WriteEndArray();
                break;

            default:
                writer.WriteStringValue(value.ToString());
                break;
        }
    }
}

/// <summary>会话恢复数据的 JSON 绑定。</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LlmExecutionState))]
internal sealed partial class LlmExecutionStateJson : JsonSerializerContext;
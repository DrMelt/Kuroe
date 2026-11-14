using System.Text.Json;
using System.Text.Json.Serialization;
using Kuroe.Execution.Shared;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Kuroe.Node.Llm.Sessions;

/// <summary>一次执行的 MAF 会话快照与执行使用的模型。</summary>
internal sealed record LlmAgentSessionPayload(
    string Model,
    JsonElement Session) : IExecutionRestoreData;

/// <summary>MAF 会话快照的编解码与恢复判定。</summary>
internal static class AgentSessionCodec
{
    /// <summary>当前会话恢复数据的格式名。</summary>
    internal const string Format = "llm.agent.session.v1";

    /// <summary>把会话写成执行记录中的恢复负载。</summary>
    internal static ExecutionRestoreState Encode(LlmAgentSessionPayload payload) =>
        new(Format, JsonSerializer.Serialize(payload, LlmAgentSessionPayloadJson.Default.LlmAgentSessionPayload));

    /// <summary>生成项目控制的会话负载，会话快照由 MAF 负责。</summary>
    internal static async Task<LlmAgentSessionPayload> CaptureAsync(
        AIAgent agent,
        AgentSession session,
        string model,
        CancellationToken cancellationToken)
    {
        JsonElement serialized = await agent
            .SerializeSessionAsync(session, null, cancellationToken)
            .ConfigureAwait(false);

        return new LlmAgentSessionPayload(model, serialized.Clone());
    }

    /// <summary>从执行记录中的恢复负载读回。</summary>
    internal static LlmAgentSessionPayload Decode(ExecutionRestoreState data)
    {
        if (data.Format != Format)
        {
            throw new FormatException($"不支持的会话恢复数据格式 {data.Format}。");
        }

        LlmAgentSessionPayload payload = JsonSerializer.Deserialize(
            data.Payload,
            LlmAgentSessionPayloadJson.Default.LlmAgentSessionPayload)
            ?? throw new FormatException("会话恢复数据为空。");

        if (string.IsNullOrWhiteSpace(payload.Model))
        {
            throw new FormatException("会话恢复数据缺少模型。");
        }

        if (payload.Session.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("会话恢复数据缺少会话状态。");
        }

        return payload;
    }

    /// <summary>从会话负载恢复 MAF 会话。</summary>
    internal static async Task<AgentSession> RestoreAsync(
        AIAgent agent,
        LlmAgentSessionPayload payload,
        CancellationToken cancellationToken) =>
        await agent
            .DeserializeSessionAsync(payload.Session, null, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>历史末尾与工具调用配对是否允许从下一次请求恢复。</summary>
    internal static bool CanContinue(AgentSession session) =>
        session.TryGetInMemoryChatHistory(out List<ChatMessage>? messages)
        && messages is not null
        && CanContinue(messages);

    /// <summary>一组消息是否可以从下一次请求恢复。</summary>
    internal static bool CanContinue(IReadOnlyList<ChatMessage> messages)
    {
        if (messages.Count == 0)
        {
            return false;
        }

        HashSet<string> pendingCalls = [];
        foreach (ChatMessage message in messages)
        {
            foreach (AIContent content in message.Contents)
            {
                switch (content)
                {
                    case FunctionCallContent call when call.CallId is { Length: > 0 } callId:
                        pendingCalls.Add(callId);
                        break;

                    case FunctionResultContent result when result.CallId is { Length: > 0 } callId:
                        pendingCalls.Remove(callId);
                        break;

                    case TextContent or TextReasoningContent:
                        break;
                }
            }
        }

        if (pendingCalls.Count > 0)
        {
            return false;
        }

        ChatMessage last = messages[^1];
        if (last.Role == ChatRole.User || last.Role == ChatRole.Tool)
        {
            return true;
        }

        return last.Role == ChatRole.Assistant
            && !last.Contents.OfType<FunctionCallContent>().Any()
            && last.Contents.Any(content => content is TextContent or TextReasoningContent);
    }
}

/// <summary>会话恢复负载的 JSON 绑定。</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LlmAgentSessionPayload))]
internal sealed partial class LlmAgentSessionPayloadJson : JsonSerializerContext;
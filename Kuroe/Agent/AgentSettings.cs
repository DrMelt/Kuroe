using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ApiHub.Shared.Models;
using ErrorOr;
using Microsoft.Extensions.Logging;

namespace Kuroe.Agent;

/// <summary>对话与请求参数，绑定自用户层的 Agent 节。接入信息由目录提供，不在此处。</summary>
internal sealed record AgentSettings
{
    public const string SectionName = "Agent";

    /// <summary>模型设置在用户层中的路径。</summary>
    public const string ModelPath = $"{SectionName}:{nameof(Model)}";

    public const string DefaultSystemPrompt = "你是一个可以使用工具获取实时信息并解答问题的助手。";

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>当前选用的模型名，必须在目录中注册。空或全空白的文本视为未选择，由调用方给出选择指引。</summary>
    public string? Model { get; init; }

    public string SystemPrompt { get; init; } = DefaultSystemPrompt;

    /// 低于该级别的日志不产生输出。
    public LogLevel LogLevel { get; init; } = LogLevel.Warning;

    public float? Temperature { get; init; }

    public int? MaxOutputTokens { get; init; }

    /// <summary>模型或系统提示词变化后旧上下文不再适用。凭据与端点变化不影响历史，客户端由 <see cref="AgentClientProvider"/> 按连接重建。</summary>
    public bool InvalidatesHistory(AgentSettings other) => Model != other.Model || SystemPrompt != other.SystemPrompt;

    /// <summary>日志级别在启动时写入日志管道，改动重启后生效。</summary>
    public bool RequiresRestart(AgentSettings other) => LogLevel != other.LogLevel;

    /// <summary>绑定并校验该节，节缺失时全部取默认值。类型转换交给 JSON 反序列化，值的合法性交给 ApiHub 的值对象。</summary>
    public static ErrorOr<AgentSettings> From(JsonObject? section)
    {
        Raw? raw;
        try
        {
            raw = section?.Deserialize<Raw>(ReadOptions);
        }
        catch (JsonException ex)
        {
            return [AgentErrors.Bind(ex.Message)];
        }

        string? model = null;
        if (!string.IsNullOrWhiteSpace(raw?.Model))
        {
            ErrorOr<ModelName> parsed = ModelName.Create(raw.Model);
            if (parsed.IsError)
            {
                return parsed.ErrorsOrEmptyList;
            }

            model = parsed.Value.Value;
        }

        return new AgentSettings
        {
            Model = model,
            SystemPrompt = raw?.SystemPrompt ?? DefaultSystemPrompt,
            LogLevel = raw?.LogLevel ?? LogLevel.Warning,
            Temperature = raw?.Temperature,
            MaxOutputTokens = raw?.MaxOutputTokens,
        };
    }

    /// <summary>用户层里的原始形状，只承载类型转换。</summary>
    private sealed class Raw
    {
        public string? Model { get; init; }

        public string? SystemPrompt { get; init; }

        public LogLevel? LogLevel { get; init; }

        public float? Temperature { get; init; }

        public int? MaxOutputTokens { get; init; }
    }
}


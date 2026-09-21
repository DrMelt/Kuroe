using ApiHub.Models;
using ErrorOr;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kuroe.Agent;

/// <summary>对话与请求参数，绑定自配置文件的 Agent 节。接入信息由目录提供，不在此处。</summary>
public sealed record AgentSettings
{
    public const string SectionName = "Agent";

    public const string DefaultSystemPrompt = "你是一个可以使用工具获取实时信息并解答问题的助手。";

    /// <summary>当前选用的模型，必须在目录中注册。</summary>
    public required ModelName Model { get; init; }

    public string SystemPrompt { get; init; } = DefaultSystemPrompt;

    /// 低于该级别的日志不产生输出。
    public LogLevel LogLevel { get; init; } = LogLevel.Warning;

    public float? Temperature { get; init; }

    public int? MaxOutputTokens { get; init; }

    /// <summary>模型或系统提示词变化后旧上下文不再适用。凭据与端点变化不影响历史，客户端由 <see cref="AgentClientProvider"/> 按连接重建。</summary>
    public bool InvalidatesSession(AgentSettings other) => Model != other.Model || SystemPrompt != other.SystemPrompt;

    /// <summary>日志级别在启动时写入日志管道，改动重启后生效。</summary>
    public bool RequiresRestart(AgentSettings other) => LogLevel != other.LogLevel;

    /// <summary>绑定并校验该节。类型转换交给配置绑定器，值的合法性交给 ApiHub 的值对象。</summary>
    public static ErrorOr<AgentSettings> From(IConfiguration configuration)
    {
        Raw? raw;
        try
        {
            raw = configuration.Get<Raw>();
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            return [Error.Validation($"{SectionName}.Bind", ex.Message)];
        }

        ErrorOr<ModelName> model = ModelName.Create(raw?.Model ?? string.Empty);
        if (model.IsError)
        {
            return model.ErrorsOrEmptyList;
        }

        return new AgentSettings
        {
            Model = model.Value,
            SystemPrompt = raw?.SystemPrompt ?? DefaultSystemPrompt,
            LogLevel = raw?.LogLevel ?? LogLevel.Warning,
            Temperature = raw?.Temperature,
            MaxOutputTokens = raw?.MaxOutputTokens,
        };
    }

    /// <summary>配置文件里的原始形状，只承载类型转换。</summary>
    private sealed class Raw
    {
        public string? Model { get; init; }

        public string? SystemPrompt { get; init; }

        public LogLevel? LogLevel { get; init; }

        public float? Temperature { get; init; }

        public int? MaxOutputTokens { get; init; }
    }
}


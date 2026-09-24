using System.Text.Json;
using System.Text.Json.Nodes;
using ApiHub.Shared.Models;
using ErrorOr;
using Microsoft.Extensions.Logging;

namespace Kuroe.Configuration;

/// <summary>对话与请求参数，绑定自用户层的 Agent 节。接入信息由目录提供，不在此处。
/// 各项默认值只有属性初始化器一处，用户层未写的项取它。</summary>
internal sealed record AgentSettings
{
    public const string SectionName = "Agent";

    /// <summary>模型设置在用户层中的路径。</summary>
    public const string ModelPath = $"{SectionName}:{nameof(Model)}";

    public const string DefaultSystemPrompt = "你是一个可以使用工具获取实时信息并解答问题的助手。";

    /// <summary>当前选用的模型名，必须在目录中注册。空或全空白的文本视为未选择，由调用方给出选择指引。</summary>
    public string? Model { get; init; }

    public string SystemPrompt { get; init; } = DefaultSystemPrompt;

    /// 低于该级别的日志不产生输出。
    public LogLevel LogLevel { get; init; } = LogLevel.Warning;

    public float? Temperature { get; init; }

    public int? MaxOutputTokens { get; init; }

    /// <summary>同时在跑的 agent 数上限，超出的排队等待。改动即时生效。</summary>
    public int MaxConcurrentRuns { get; init; } = 4;

    /// <summary>提交任务时未指定流程则用该流程，未设置时用内置流程。</summary>
    public string? DefaultFlow { get; init; }

    /// <summary>单个条目允许的实施轮数上限，检查步骤的 MaxAttempts 不得超过它。</summary>
    public int MaxAttempts { get; init; } = 3;

    /// <summary>模型或系统提示词变化后旧上下文不再适用。凭据与端点变化不影响历史，客户端由 <see cref="Agent.Sessions.AgentClientProvider"/> 按模型重建。</summary>
    public bool InvalidatesHistory(AgentSettings other) => Model != other.Model || SystemPrompt != other.SystemPrompt;

    /// <summary>日志级别在启动时写入日志管道，改动重启后生效。</summary>
    public bool RequiresRestart(AgentSettings other) => LogLevel != other.LogLevel;

    /// <summary>绑定并校验该节，未写的设置项取属性初始化器的默认值。值的合法性交给 ApiHub 的值对象。</summary>
    public static ErrorOr<AgentSettings> From(JsonObject? section)
    {
        AgentSectionDto? file;
        try
        {
            file = section?.Deserialize(SettingsJson.Default.AgentSectionDto);
        }
        catch (JsonException ex)
        {
            return [SettingsErrors.Bind(ex.Message)];
        }

        if (file is null)
        {
            return new AgentSettings();
        }

        AgentSettings defaults = new();
        AgentSettings bound = defaults with
        {
            Model = file.Model,
            SystemPrompt = file.SystemPrompt ?? defaults.SystemPrompt,
            LogLevel = file.LogLevel ?? defaults.LogLevel,
            Temperature = file.Temperature,
            MaxOutputTokens = file.MaxOutputTokens,
            MaxConcurrentRuns = file.MaxConcurrentRuns ?? defaults.MaxConcurrentRuns,
            DefaultFlow = file.DefaultFlow,
            MaxAttempts = file.MaxAttempts ?? defaults.MaxAttempts,
        };

        if (string.IsNullOrWhiteSpace(bound.Model))
        {
            return bound with { Model = null };
        }

        ErrorOr<ModelName> parsed = ModelName.Create(bound.Model);

        return parsed.IsError
            ? parsed.ErrorsOrEmptyList
            : bound with { Model = parsed.Value.Value };
    }
}

/// <summary>用户层里 Agent 节的形状。未写的项为 null，绑定成 <see cref="AgentSettings"/> 时取它的默认值。</summary>
internal sealed class AgentSectionDto
{
    public string? Model { get; set; }

    public string? SystemPrompt { get; set; }

    public LogLevel? LogLevel { get; set; }

    public float? Temperature { get; set; }

    public int? MaxOutputTokens { get; set; }

    public int? MaxConcurrentRuns { get; set; }

    public string? DefaultFlow { get; set; }

    public int? MaxAttempts { get; set; }
}


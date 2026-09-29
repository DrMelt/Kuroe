using System.Text.Json;
using System.Text.Json.Nodes;
using ApiHub.Shared.Models;
using ErrorOr;
using Microsoft.Extensions.Logging;

namespace Kuroe.Configuration;

/// <summary>运行偏好与请求参数，绑定自用户层的 Runtime 节。接入信息由目录提供，不在此处。
/// 各项默认值只有属性初始化器一处，用户层未写的项取它。</summary>
internal sealed record RuntimeSettings
{
    public const string SectionName = "Runtime";

    /// <summary>模型设置在用户层中的路径。</summary>
    public const string ModelPath = $"{SectionName}:{nameof(Model)}";

    /// <summary>当前选用的模型名，必须在目录中注册。空或全空白的文本视为未选择，由调用方给出选择指引。</summary>
    public string? Model { get; init; }

    /// 低于该级别的日志不产生输出。
    public LogLevel LogLevel { get; init; } = LogLevel.Warning;

    public float? Temperature { get; init; }

    public int? MaxOutputTokens { get; init; }

    /// <summary>同时在跑的 run 数上限，超出的排队等待。改动即时生效。</summary>
    public int MaxConcurrentRuns { get; init; } = 4;

    /// <summary>提交任务时未指定流程则用该流程，未设置时用内置流程。</summary>
    public string? DefaultFlow { get; init; }

    /// <summary>模型变化后旧上下文不再适用。凭据与端点变化不影响历史，会话客户端由 <see cref="Executions.Sessions.ClientProvider"/> 按模型重建。</summary>
    public bool InvalidatesHistory(RuntimeSettings other) => Model != other.Model;

    /// <summary>日志级别在启动时写入日志管道，改动重启后生效。</summary>
    public bool RequiresRestart(RuntimeSettings other) => LogLevel != other.LogLevel;

    /// <summary>绑定并校验该节，未写的设置项取属性初始化器的默认值。值的合法性交给 ApiHub 的值对象。</summary>
    public static ErrorOr<RuntimeSettings> From(JsonObject? section)
    {
        RuntimeSectionDto? file;
        try
        {
            file = section?.Deserialize(SettingsJson.Default.RuntimeSectionDto);
        }
        catch (JsonException ex)
        {
            return [SettingsErrors.Bind(ex.Message)];
        }

        if (file is null)
        {
            return new RuntimeSettings();
        }

        RuntimeSettings defaults = new();
        RuntimeSettings bound = defaults with
        {
            Model = file.Model,
            LogLevel = file.LogLevel ?? defaults.LogLevel,
            Temperature = file.Temperature,
            MaxOutputTokens = file.MaxOutputTokens,
            MaxConcurrentRuns = file.MaxConcurrentRuns ?? defaults.MaxConcurrentRuns,
            DefaultFlow = file.DefaultFlow,
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

/// <summary>用户层里 Runtime 节的形状。未写的项为 null，绑定成 <see cref="RuntimeSettings"/> 时取它的默认值。</summary>
internal sealed class RuntimeSectionDto
{
    public string? Model { get; set; }

    public LogLevel? LogLevel { get; set; }

    public float? Temperature { get; set; }

    public int? MaxOutputTokens { get; set; }

    public int? MaxConcurrentRuns { get; set; }

    public string? DefaultFlow { get; set; }
}


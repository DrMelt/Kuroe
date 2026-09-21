using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ApiHub.Models;
using ErrorOr;
using Kuroe.Agent;
using Kuroe.Configuration;

namespace Kuroe.Cli.Commands;

/// <summary>/config、/set、/unset 的解析与执行。模型路径由 /model 管理，这里拒绝。</summary>
internal sealed class SettingsCommands(SettingsProvider settings)
{
    /// <summary>该命令族的帮助行。</summary>
    public static IReadOnlyList<(string Command, string Description)> Help { get; } =
    [
        ("/config", "打印生效配置，标记各项是否已写入用户层"),
        ("/set <路径> <值>", "写入用户层并立即生效"),
        ("/unset <路径>", "删除用户层中的该项"),
    ];

    private static readonly JsonSerializerOptions ViewOptions = new()
    {
        Converters = { new JsonStringEnumConverter(), new ModelNameJsonConverter() },
    };

    private readonly SettingsProvider _settings = settings;

    /// <summary>写入用户层中的设置项并立即生效。</summary>
    public void Set(string[] parts)
    {
        if (parts.Length != 3)
        {
            Console.WriteLine("用法：/set <路径> <值>，值含空格时用双引号包起来，例如 /set Agent:Temperature 0.7");
            return;
        }

        JsonNode? value = ParseValue(parts[2]);
        if (value is null)
        {
            Console.WriteLine("值不能为 null，清除设置请用 /unset。");
            return;
        }

        ErrorOr<string> path = Resolve(parts[1]);
        if (path.IsError)
        {
            ConsoleResults.Reject(path.ErrorsOrEmptyList);
            return;
        }

        ConsoleResults.Apply(_settings, () => _settings.Set(path.Value, value));
    }

    /// <summary>删除用户层中的该项。</summary>
    public void Unset(string[] parts)
    {
        if (parts.Length != 2)
        {
            Console.WriteLine("用法：/unset <路径>，例如 /unset Agent:Temperature");
            return;
        }

        ErrorOr<string> resolved = Resolve(parts[1]);
        if (resolved.IsError)
        {
            ConsoleResults.Reject(resolved.ErrorsOrEmptyList);
            return;
        }

        if (_settings.TryGetUserValue(resolved.Value) is null)
        {
            Console.WriteLine($"{resolved.Value} 未在用户层设置。");
            return;
        }

        ConsoleResults.Apply(_settings, () => _settings.Clear(resolved.Value));
    }

    /// <summary>打印生效的配置，逐项标记是否来自用户层。</summary>
    public void PrintConfig()
    {
        Console.WriteLine($"用户层文件：{_settings.UserSettingsFile}");

        JsonObject view = JsonSerializer.SerializeToNode(_settings.Current, ViewOptions)!.AsObject();
        foreach ((string section, JsonNode? body) in view)
        {
            Console.WriteLine($"{section}:");
            foreach ((string key, JsonNode? value) in body!.AsObject())
            {
                string text = value is JsonValue scalar ? scalar.ToString() : "未设置";
                string path = $"{section}:{key}";
                bool written = _settings.TryGetUserValue(path) is not null;

                Console.WriteLine($"  {key} = {text}（{(written ? "用户层已设置" : "用户层未设置")}）");
            }
        }
    }

    /// <summary>解析路径为规范形式。路径未定义或由 /model 管理时返回错误，错误描述即给用户的提示。</summary>
    private static ErrorOr<string> Resolve(string path)
    {
        ErrorOr<string> resolved = KuroeSettings.ResolvePath(path);
        if (resolved.IsError)
        {
            return resolved;
        }

        return resolved.Value == AgentSettings.ModelPath
            ? Error.Validation("Command.ModelPath", "模型用 /model 管理：/model <模型> 切换，/model none 取消选择。")
            : resolved.Value;
    }

    /// <summary>值先按 JSON 字面量解析，解析失败按字符串写入。</summary>
    private static JsonNode? ParseValue(string text)
    {
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return JsonValue.Create(text);
        }
    }

    /// <summary>让模型名在 /config 中显示为原文。</summary>
    private sealed class ModelNameJsonConverter : JsonConverter<ModelName>
    {
        public override ModelName Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, ModelName value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }
}
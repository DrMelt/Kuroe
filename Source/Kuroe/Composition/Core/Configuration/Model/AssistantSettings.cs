using System.Text.Json;
using System.Text.Json.Nodes;
using ErrorOr;
using Kuroe.Composition.Configuration.Files;

namespace Kuroe.Composition.Configuration.Model;

/// <summary>内置助手的配置，绑定自用户层的 Assistant 节。未写的项取属性初始化器的默认值。</summary>
internal sealed record AssistantSettings
{
    public const string SectionName = "Assistant";

    public const string DefaultLanguage = "中文";

    /// <summary>内置助手的回复语言，作为系统指令的一部分。</summary>
    public string Language { get; init; } = DefaultLanguage;

    /// <summary>语言变化只影响新建会话，已有会话保持建立时的语言。</summary>
    public bool AffectsNewDialogueSessions(AssistantSettings other) => Language != other.Language;

    /// <summary>绑定并校验该节，未写的语言取默认值，空白值拒绝。</summary>
    public static ErrorOr<AssistantSettings> From(JsonObject? section)
    {
        AssistantSectionDto? file;
        try
        {
            file = section?.Deserialize(SettingsJson.Default.AssistantSectionDto);
        }
        catch (JsonException ex)
        {
            return [SettingsErrors.Bind(ex.Message)];
        }

        if (file?.Language is null)
        {
            return new AssistantSettings();
        }

        string language = file.Language.Trim();

        return language.Length == 0
            ? [SettingsErrors.Language()]
            : new AssistantSettings { Language = language };
    }
}

/// <summary>用户层里 Assistant 节的形状。未写的项为 null，绑定成 <see cref="AssistantSettings"/> 时取它的默认值。</summary>
internal sealed class AssistantSectionDto
{
    public string? Language { get; set; }
}
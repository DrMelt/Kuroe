using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kuroe.Configuration;

/// <summary>用户层设置的 JSON 绑定。读法由源生成固定，不经反射式序列化。
/// 被绑定的文件形状必须用可写属性，否则缺键的项拿不到声明的默认值。</summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(AgentSectionDto))]
internal sealed partial class SettingsJson : JsonSerializerContext;

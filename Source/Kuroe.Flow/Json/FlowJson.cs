using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kuroe.Flow.Json.Files;

namespace Kuroe.Flow.Json;

/// <summary>流程文件的 JSON 绑定。字段形状与 null 省略由源生成选项声明，写侧选项是独立实例，在同口径下只改缩进与转义。
/// 节点之外的未定义字段被拒绝，节点对象里类型不承认的字段由文件层按类型的字段表报出。</summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(FlowFileDto))]
[JsonSerializable(typeof(PortEntryDto))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class FlowJson : JsonSerializerContext
{
    private static readonly Lazy<JsonSerializerOptions> LazyWrite = new(static () => new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = Default,
    });

    /// <summary>写侧选项：与源生成选项同口径省略 null 字段，额外缩进输出与不转义非 ASCII 字符，元数据取自本上下文。</summary>
    internal static JsonSerializerOptions WriteOptions => LazyWrite.Value;
}

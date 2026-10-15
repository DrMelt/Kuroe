using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kuroe.Flow.Json.Files;

namespace Kuroe.Flow.Json;

/// <summary>流程文件的 JSON 绑定。读法由源生成固定，本上下文的写侧选项只改缩进与转义，
/// 字段是否写出由 DTO 上的特性决定。节点之外的未定义字段被拒绝，节点对象里类型不承认的字段由文件层按类型的字段表报出。</summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    UseStringEnumConverter = true)]
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
        TypeInfoResolver = Default,
    });

    /// <summary>写侧选项：缩进输出、非 ASCII 字符不转义，元数据仍取自本上下文。</summary>
    internal static JsonSerializerOptions WriteOptions => LazyWrite.Value;
}

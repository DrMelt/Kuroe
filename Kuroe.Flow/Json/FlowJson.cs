using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kuroe.Flow.Shared.Files;

namespace Kuroe.Flow.Json;

/// <summary>流程文件的 JSON 绑定。读法由源生成固定，本上下文的写侧选项只改缩进与转义，
/// 字段是否写出由 DTO 上的特性决定。文件里出现未定义字段即拒绝，去掉的字段不会被静默忽略。</summary>
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

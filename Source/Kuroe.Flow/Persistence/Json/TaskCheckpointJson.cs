using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kuroe.Flow.Persistence.Json;

/// <summary>检查点文件的 JSON 绑定。读取拒绝未定义字段，写出省略 null 字段并保留非 ASCII 字符。</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(TaskCheckpointFileDto))]
internal sealed partial class TaskCheckpointJson : JsonSerializerContext
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

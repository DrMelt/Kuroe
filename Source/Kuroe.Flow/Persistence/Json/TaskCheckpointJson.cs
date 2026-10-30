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
}

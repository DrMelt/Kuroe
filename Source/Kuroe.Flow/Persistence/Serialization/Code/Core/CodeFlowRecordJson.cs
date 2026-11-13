using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kuroe.Flow.Persistence.Code;

/// <summary>代码流程存档片段的 JSON 绑定。读取拒绝未定义字段，写出省略 null 字段。</summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CodeFlowDefinitionData))]
[JsonSerializable(typeof(CodeFlowStateData))]
internal sealed partial class CodeFlowRecordJson : JsonSerializerContext;
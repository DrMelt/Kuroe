using System.Text.Json.Serialization;

namespace Kuroe.Flow.Execution.Submissions;

/// <summary>规划交回的条目的 JSON 绑定。读法由源生成固定，不经反射式序列化。</summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(List<WorkItemDto>))]
internal sealed partial class WorkItemJson : JsonSerializerContext;

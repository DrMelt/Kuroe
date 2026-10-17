using System.Text.Json.Serialization;

namespace Kuroe.Flow.Execution.Submissions;

/// <summary>输出端口交回的 JSON 绑定。读法由源生成固定，不经反射式序列化。</summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class PortJson : JsonSerializerContext;

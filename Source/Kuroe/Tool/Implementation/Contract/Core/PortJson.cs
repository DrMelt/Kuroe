using System.Text.Json.Serialization;

namespace Kuroe.Tool.Contract;

/// <summary>输出端口交回的 JSON 绑定。读法由源生成固定，不经反射式序列化。
/// 每个端口的取值形态由声明类型决定，解析后得到字符串、布尔、空值或对象。</summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(Dictionary<string, System.Text.Json.JsonElement>))]
internal sealed partial class PortJson : JsonSerializerContext;
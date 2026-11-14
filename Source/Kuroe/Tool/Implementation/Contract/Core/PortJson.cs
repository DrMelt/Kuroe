using System.Text.Json.Serialization;

namespace Kuroe.Tool.Contract;

/// <summary>输出端口交回的 JSON 绑定。读法由源生成固定，不经反射式序列化。
/// 顶层键是端口名，按原文保留；每个端口的取值形态由声明决定，解析后得到字符串或对象。</summary>
[JsonSourceGenerationOptions(AllowTrailingCommas = true)]
[JsonSerializable(typeof(Dictionary<string, System.Text.Json.JsonElement>))]
internal sealed partial class PortJson : JsonSerializerContext;
using System.Text.Json;
using Kuroe.Flow.Json.Files;

namespace Kuroe.Flow.Json;

/// <summary>流程文件的文本形态：文本与文件 DTO 之间的读写。读法由源生成固定，不经反射式序列化。</summary>
public static class FlowFileJson
{
    /// <summary>把文件文本读成 DTO，形状不合法时抛 <see cref="JsonException"/>，内容是 JSON null 时为空。</summary>
    public static FlowFileDto? Parse(string text) => JsonSerializer.Deserialize(text, FlowJson.Default.FlowFileDto);

    /// <summary>把 DTO 写成文件文本，缩进输出、非 ASCII 字符不转义。</summary>
    public static string Serialize(FlowFileDto file) =>
        JsonSerializer.Serialize(file, FlowJson.WriteOptions.GetTypeInfo(typeof(FlowFileDto)));
}
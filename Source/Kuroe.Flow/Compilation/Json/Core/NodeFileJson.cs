using System.Text.Json;
using Kuroe.Flow.Json.Files;

namespace Kuroe.Flow.Json;

/// <summary>节点库文件的文本形态：文本与文件 DTO 之间的读写。读法由源生成固定，不经反射式序列化。</summary>
public static class NodeFileJson
{
    /// <summary>把节点库文件文本读成 DTO，形状不合法时抛 <see cref="JsonException"/>，内容是 JSON null 时为空。</summary>
    public static NodeFileDto? Parse(string text) => JsonSerializer.Deserialize(text, FlowJson.Default.NodeFileDto);

    /// <summary>把节点库文件 DTO 写成文本，缩进输出、非 ASCII 字符不转义。</summary>
    public static string Serialize(NodeFileDto file) =>
        JsonSerializer.Serialize(file, FlowJson.WriteOptions.GetTypeInfo(typeof(NodeFileDto)));
}
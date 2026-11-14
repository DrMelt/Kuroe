using Kuroe.Tasks.Shared;
using System.Text.Json;
using ErrorOr;
using Kuroe.Flow.Compilation.Shared;
using Kuroe.Flow.Json.Files;
using Kuroe.Flow.Json.Shared;

namespace Kuroe.Flow.Json;

/// <summary>节点库文件的 JSON 格式实现：文本与节点定义之间的绑定，不构造模型。</summary>
public sealed class JsonNodeFormat : INodeFormatReader, INodeFormatWriter
{
    /// <summary>读入节点库文件。语法错误给格式错误，文件形状错误逐条给全。</summary>
    public ErrorOr<IReadOnlyList<NodeDefinitionDocument>> Read(string text)
    {
        NodeFileDto? file;
        try
        {
            file = NodeFileJson.Parse(text);
        }
        catch (JsonException ex)
        {
            return Error.Validation(FlowErrorCodes.FlowFormat, $"节点文件不是合法 JSON：{ex.Message}。节点文件只写 Nodes，流程写在 flows.json。");
        }

        if (file is null)
        {
            return Error.Validation(FlowErrorCodes.FlowFormat, "节点文件是空内容。");
        }

        return FlowFileReader.ReadNodes(file);
    }

    /// <summary>写出节点库文件。节点定义必须已经是规范形态。</summary>
    public ErrorOr<string> Write(IReadOnlyList<NodeDefinitionDocument> nodes)
    {
        try
        {
            return NodeFileJson.Serialize(FlowFileWriter.WriteNodes(nodes));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return Error.Validation(FlowErrorCodes.FlowFormat, $"节点文件写出失败：{ex.Message}");
        }
    }
}
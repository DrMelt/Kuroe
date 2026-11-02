using System.Text.Json;
using ErrorOr;
using Kuroe.Flow.Compilation.Shared;
using Kuroe.Flow.Json.Files;
using Kuroe.Flow.Json.Shared;
using Kuroe.Flow.Shared;

namespace Kuroe.Flow.Json;

/// <summary>流程文件的 JSON 格式实现：文本与流程描述之间的绑定，不构造模型。</summary>
public sealed class JsonFlowFormat : IFlowFormatReader, IFlowFormatWriter
{
    /// <summary>读入流程文件。语法错误给格式错误，文件形状错误逐条给全。</summary>
    public ErrorOr<IReadOnlyList<FlowDocumentFlow>> Read(string text)
    {
        FlowFileDto? file;
        try
        {
            file = FlowFileJson.Parse(text);
        }
        catch (JsonException ex)
        {
            return Error.Validation(FlowErrorCodes.FlowFormat, $"流程文件不是合法 JSON：{ex.Message}。流程文件只写 Flows，节点库写在 nodes.json。");
        }

        if (file is null)
        {
            return Error.Validation(FlowErrorCodes.FlowFormat, "流程文件是空内容。");
        }

        return FlowFileReader.ReadFlows(file);
    }

    /// <summary>写出流程文件。流程描述必须已经是规范形态。</summary>
    public ErrorOr<string> Write(IReadOnlyList<FlowDocumentFlow> flows)
    {
        try
        {
            return FlowFileJson.Serialize(FlowFileWriter.WriteFlows(flows));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return Error.Validation(FlowErrorCodes.FlowFormat, $"流程文件写出失败：{ex.Message}");
        }
    }
}
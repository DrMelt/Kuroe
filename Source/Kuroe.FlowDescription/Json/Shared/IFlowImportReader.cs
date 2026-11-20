using ErrorOr;

namespace Kuroe.FlowDescription.Json.Shared;

/// <summary>导入文件的读取：按顶层字段判定节点库文件或流程文件。</summary>
public interface IFlowImportReader
{
    /// <summary>读入导入文件，类别不明确时给出格式错误。</summary>
    ErrorOr<FlowImportContent> Read(string text);
}
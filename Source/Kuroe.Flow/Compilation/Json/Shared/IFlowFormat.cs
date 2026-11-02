using ErrorOr;
using Kuroe.Flow.Compilation.Shared;

namespace Kuroe.Flow.Json.Shared;

/// <summary>节点库文件的读入：把承载格式转成节点定义，错误按文件位置或节点作用域给出。</summary>
public interface INodeFormatReader
{
    /// <summary>读入节点库文本，形状不合法时给出格式错误。</summary>
    ErrorOr<IReadOnlyList<NodeDefinitionDocument>> Read(string text);
}

/// <summary>节点库文件的写出：把节点定义转回承载格式。</summary>
public interface INodeFormatWriter
{
    /// <summary>写出节点库，失败时给出格式错误。</summary>
    ErrorOr<string> Write(IReadOnlyList<NodeDefinitionDocument> nodes);
}

/// <summary>流程文件的读入：把承载格式转成流程描述，错误按文件位置或流程作用域给出。</summary>
public interface IFlowFormatReader
{
    /// <summary>读入流程文本，形状不合法时给出格式错误。</summary>
    ErrorOr<IReadOnlyList<FlowDocumentFlow>> Read(string text);
}

/// <summary>流程文件的写出：把流程描述转回承载格式。</summary>
public interface IFlowFormatWriter
{
    /// <summary>写出流程，失败时给出格式错误。</summary>
    ErrorOr<string> Write(IReadOnlyList<FlowDocumentFlow> flows);
}

/// <summary>导入文件的类别。</summary>
public enum FlowImportKind
{
    /// <summary>节点库文件。</summary>
    Nodes,

    /// <summary>流程文件。</summary>
    Flows,
}

/// <summary>导入文件的类别与内容，只承载其中一类。</summary>
public sealed record FlowImportContent(
    FlowImportKind Kind,
    IReadOnlyList<NodeDefinitionDocument> Nodes,
    IReadOnlyList<FlowDocumentFlow> Flows);

/// <summary>导入文件的读取：按顶层字段判定节点库文件或流程文件。</summary>
public interface IFlowImportReader
{
    /// <summary>读入导入文件，类别不明确时给出格式错误。</summary>
    ErrorOr<FlowImportContent> Read(string text);
}
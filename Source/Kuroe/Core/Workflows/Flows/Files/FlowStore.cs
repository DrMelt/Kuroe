using ErrorOr;
using Kuroe.Flow.Compilation.Shared;
using Kuroe.Flow.Json.Shared;
using Kuroe.Flow.Persistence.FileSystem;

namespace Kuroe.Workflows.Flows.Files;

/// <summary>nodes.json 与 flows.json 的文件存取：路径解析、读取与原子写入。文本与描述文档的转换由格式实现承担。</summary>
sealed class FlowStore(
    string nodesFile,
    string flowsFile,
    string baseDirectory,
    INodeFormatReader nodeReader,
    INodeFormatWriter nodeWriter,
    IFlowFormatReader flowReader,
    IFlowFormatWriter flowWriter,
    IFlowImportReader importReader)
{
    private readonly string _nodesFile = Path.GetFullPath(nodesFile);
    private readonly string _flowsFile = Path.GetFullPath(flowsFile);
    private readonly string _baseDirectory = Path.GetFullPath(baseDirectory);

    /// <summary>工作目录根，用户给出的文件参数以此为基准。</summary>
    public string BaseDirectory => _baseDirectory;

    /// <summary>把文件参数解析为绝对路径，相对参数按工作目录解析。</summary>
    public ErrorOr<string> Resolve(string path)
    {
        try
        {
            return Path.GetFullPath(path, BaseDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            return [FlowFileErrors.InvalidPath(path, ex.Message)];
        }
    }

    /// <summary>读取节点库与流程文件，两个文件都不存在时为空。</summary>
    public ErrorOr<FlowDocument?> Load()
    {
        bool hasNodes = File.Exists(_nodesFile);
        bool hasFlows = File.Exists(_flowsFile);
        if (!hasNodes && !hasFlows)
        {
            return (FlowDocument?)null;
        }

        List<NodeDefinitionDocument> nodes = [];
        if (hasNodes)
        {
            ErrorOr<string> text = ReadText(_nodesFile);
            if (text.IsError)
            {
                return text.ErrorsOrEmptyList;
            }

            ErrorOr<IReadOnlyList<NodeDefinitionDocument>> read = ReadNodesText(text.Value);
            if (read.IsError)
            {
                return InFile(_nodesFile, read.ErrorsOrEmptyList);
            }

            nodes.AddRange(read.Value);
        }

        List<FlowDocumentFlow> flows = [];
        if (hasFlows)
        {
            ErrorOr<string> text = ReadText(_flowsFile);
            if (text.IsError)
            {
                return text.ErrorsOrEmptyList;
            }

            ErrorOr<IReadOnlyList<FlowDocumentFlow>> read = ReadFlowsText(text.Value);
            if (read.IsError)
            {
                return InFile(_flowsFile, read.ErrorsOrEmptyList);
            }

            flows.AddRange(read.Value);
        }

        return new FlowDocument(nodes, flows);
    }

    /// <summary>把节点库写回 nodes.json。</summary>
    public ErrorOr<Success> SaveNodes(IReadOnlyList<NodeDefinitionDocument> nodes) =>
        Save(_nodesFile, nodeWriter.Write(nodes));

    /// <summary>把流程写回 flows.json。</summary>
    public ErrorOr<Success> SaveFlows(IReadOnlyList<FlowDocumentFlow> flows) =>
        Save(_flowsFile, flowWriter.Write(flows));

    /// <summary>把节点库写成文本，不落盘。检查点快照使用。</summary>
    public ErrorOr<string> RenderNodes(IReadOnlyList<NodeDefinitionDocument> nodes) => nodeWriter.Write(nodes);

    /// <summary>把流程写成文本，不落盘。检查点快照使用。</summary>
    public ErrorOr<string> RenderFlows(IReadOnlyList<FlowDocumentFlow> flows) => flowWriter.Write(flows);

    /// <summary>从文本读出节点库，不落盘。检查点编译使用。</summary>
    public ErrorOr<IReadOnlyList<NodeDefinitionDocument>> ReadNodesText(string text) => nodeReader.Read(text);

    /// <summary>从文本读出流程，不落盘。检查点编译使用。</summary>
    public ErrorOr<IReadOnlyList<FlowDocumentFlow>> ReadFlowsText(string text) => flowReader.Read(text);

    /// <summary>读取导入文件，按顶层字段判定节点库文件或流程文件。</summary>
    public ErrorOr<FlowImportFile> ReadFile(string path)
    {
        ErrorOr<string> text = ReadText(path);
        if (text.IsError)
        {
            return text.ErrorsOrEmptyList;
        }

        ErrorOr<FlowImportContent> content = importReader.Read(text.Value);
        if (content.IsError)
        {
            return InFile(path, content.ErrorsOrEmptyList);
        }

        return content.Value.Kind == FlowImportKind.Nodes
            ? new NodeLibraryImportFile(path, content.Value.Nodes)
            : new FlowDefinitionsImportFile(path, content.Value.Flows);
    }

    private static ErrorOr<Success> Save(string file, ErrorOr<string> rendered)
    {
        if (rendered.IsError)
        {
            return rendered.ErrorsOrEmptyList;
        }

        try
        {
            AtomicFile.WriteText(file, rendered.Value);

            return Result.Success;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [FlowFileErrors.Write(file, ex.Message)];
        }
    }

    private static ErrorOr<string> ReadText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [FlowFileErrors.Read(path, ex.Message)];
        }
    }

    private static List<Error> InFile(string path, IReadOnlyList<Error> errors) =>
        [.. errors.Select(error => FlowFileErrors.Format($"{path}：{error.Description}"))];
}

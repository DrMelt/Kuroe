using System.Text;
using ErrorOr;
using Kuroe.Files.Shared;
using Kuroe.FlowDescription.Description.Shared;
using Kuroe.FlowDescription.Json.Shared;

namespace Kuroe.Flow.Library.Files;

/// <summary>nodes.json 与 flows.json 的文件存取：路径解析、读取与原子写入。文本与流程描述的转换由格式实现承担。</summary>
public sealed class FlowStore(
    string nodesFile,
    string flowsFile,
    string baseDirectory,
    INodeFormatReader nodeReader,
    INodeFormatWriter nodeWriter,
    IFlowFormatReader flowReader,
    IFlowFormatWriter flowWriter,
    IFlowImportReader importReader,
    IWorkspaceFiles files)
{
    private readonly string _nodesFile = Path.GetFullPath(nodesFile);
    private readonly string _flowsFile = Path.GetFullPath(flowsFile);
    private readonly string _baseDirectory = Path.GetFullPath(baseDirectory);
    private readonly IWorkspaceFiles _files = files;

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
    public ErrorOr<FlowDescriptionSet?> Load()
    {
        bool hasNodes = _files.FileExists(_nodesFile);
        bool hasFlows = _files.FileExists(_flowsFile);
        if (!hasNodes && !hasFlows)
        {
            return (FlowDescriptionSet?)null;
        }

        List<NodeDefinitionDescription> nodes = [];
        if (hasNodes)
        {
            ErrorOr<string> text = ReadText(_nodesFile);
            if (text.IsError)
            {
                return text.ErrorsOrEmptyList;
            }

            ErrorOr<IReadOnlyList<NodeDefinitionDescription>> read = nodeReader.Read(text.Value);
            if (read.IsError)
            {
                return InFile(_nodesFile, read.ErrorsOrEmptyList);
            }

            nodes.AddRange(read.Value);
        }

        List<FlowEntry> flows = [];
        if (hasFlows)
        {
            ErrorOr<string> text = ReadText(_flowsFile);
            if (text.IsError)
            {
                return text.ErrorsOrEmptyList;
            }

            ErrorOr<IReadOnlyList<FlowEntry>> read = flowReader.Read(text.Value);
            if (read.IsError)
            {
                return InFile(_flowsFile, read.ErrorsOrEmptyList);
            }

            flows.AddRange(read.Value);
        }

        return new FlowDescriptionSet(nodes, flows);
    }

    /// <summary>把节点库写回 nodes.json。</summary>
    public ErrorOr<Success> SaveNodes(IReadOnlyList<NodeDefinitionDescription> nodes) =>
        Save(_nodesFile, nodeWriter.Write(nodes));

    /// <summary>把流程写回 flows.json。</summary>
    public ErrorOr<Success> SaveFlows(IReadOnlyList<FlowEntry> flows) =>
        Save(_flowsFile, flowWriter.Write(flows));

    /// <summary>读取导入文件，按顶层字段判定节点库文件还是流程文件。</summary>
    public ErrorOr<FlowImportContent> ReadFile(string path)
    {
        ErrorOr<string> text = ReadText(path);
        if (text.IsError)
        {
            return text.ErrorsOrEmptyList;
        }

        ErrorOr<FlowImportContent> content = importReader.Read(text.Value);

        return content.IsError ? InFile(path, content.ErrorsOrEmptyList) : content;
    }

    private ErrorOr<Success> Save(string file, ErrorOr<string> rendered)
    {
        if (rendered.IsError)
        {
            return rendered.ErrorsOrEmptyList;
        }

        try
        {
            _files.WriteTextAtomic(file, rendered.Value);

            return Result.Success;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [FlowFileErrors.Write(file, ex.Message)];
        }
    }

    private ErrorOr<string> ReadText(string path)
    {
        try
        {
            using StreamReader reader = new(_files.OpenRead(path), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            return reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [FlowFileErrors.Read(path, ex.Message)];
        }
    }

    private static List<Error> InFile(string path, IReadOnlyList<Error> errors) =>
        [.. errors.Select(error => FlowFileErrors.Format($"{path}：{error.Description}"))];
}

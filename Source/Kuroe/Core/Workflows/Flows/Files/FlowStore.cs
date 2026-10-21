using ErrorOr;
using Kuroe.Flow.Compiler.Shared;
using Kuroe.Flow.Json.Shared;
using Kuroe.Storage;

namespace Kuroe.Workflows.Flows.Files;

/// <summary>flows.json 的文件存取：路径解析、读取与原子写入。文本与描述文档的转换由格式实现承担。</summary>
sealed class FlowStore(string file, string baseDirectory, IFlowFormatReader reader, IFlowFormatWriter writer)
{
    private readonly string _file = Path.GetFullPath(file);
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

    /// <summary>读取流程文件为描述文档，文件不存在时为空。</summary>
    public ErrorOr<FlowDocument?> Load()
    {
        if (!File.Exists(_file))
        {
            return (FlowDocument?)null;
        }

        ErrorOr<FlowDocument> read = Read(_file);
        if (read.IsError)
        {
            return read.ErrorsOrEmptyList;
        }

        return read.Value;
    }

    /// <summary>把描述文档写回流程文件。</summary>
    public ErrorOr<Success> Save(FlowDocument document)
    {
        ErrorOr<string> text = writer.Write(document);
        if (text.IsError)
        {
            return text.ErrorsOrEmptyList;
        }

        try
        {
            AtomicFile.WriteText(_file, text.Value);

            return Result.Success;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [FlowFileErrors.Write(_file, ex.Message)];
        }
    }

    /// <summary>从指定文件读取描述文档，供导入使用。</summary>
    public ErrorOr<FlowDocument> Read(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [FlowFileErrors.Read(path, ex.Message)];
        }

        ErrorOr<FlowDocument> document = reader.Read(text);
        if (document.IsError)
        {
            return [.. document.ErrorsOrEmptyList.Select(error => FlowFileErrors.Format($"{path}：{error.Description}"))];
        }

        return document.Value;
    }
}

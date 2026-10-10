using System.Text.Json;
using ErrorOr;
using Kuroe.Storage;

namespace Kuroe.Workflows.FlowFiles;

/// <summary>flows.json 的文件存取：路径解析、读取与原子写入。文件形状的装配由 FlowAssembler 承担。</summary>
sealed class FlowStore(string file, string baseDirectory)
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

    /// <summary>读取流程文件为文件形状，文件不存在时为空。</summary>
    public ErrorOr<FlowFileDto?> LoadDto()
    {
        if (!File.Exists(_file))
        {
            return (FlowFileDto?)null;
        }

        ErrorOr<FlowFileDto> read = ReadDto(_file);
        if (read.IsError)
        {
            return read.ErrorsOrEmptyList;
        }

        return read.Value;
    }

    /// <summary>把文件形状写回流程文件。</summary>
    public ErrorOr<Success> Save(FlowFileDto file)
    {
        try
        {
            AtomicFile.WriteText(_file,
                JsonSerializer.Serialize(file, FlowJson.WriteOptions.GetTypeInfo(typeof(FlowFileDto))));

            return Result.Success;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [FlowFileErrors.Write(_file, ex.Message)];
        }
    }

    /// <summary>从指定文件读取文件形状，供导入使用。</summary>
    public static ErrorOr<FlowFileDto> ReadDto(string path)
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

        FlowFileDto? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize(text, FlowJson.Default.FlowFileDto);
        }
        catch (JsonException ex)
        {
            return [FlowFileErrors.Format($"{path}：{ex.Message}")];
        }

        if (parsed is null)
        {
            return [FlowFileErrors.Format($"{path} 是空文件。")];
        }

        return parsed;
    }
}

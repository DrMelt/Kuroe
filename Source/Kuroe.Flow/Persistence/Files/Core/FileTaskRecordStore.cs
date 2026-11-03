using ErrorOr;
using Kuroe.Flow.Persistence.Files.Shared;
using Kuroe.Flow.Persistence.Serialization.Shared;
using Kuroe.Flow.Persistence.Records.Shared;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Persistence.Files;

/// <summary>任务存档的文件存储：每个任务一个文件，写入走原子替换，读取逐文件报告失败。</summary>
public sealed class FileTaskRecordStore(
    string directory,
    ITaskRecordCodec codec) : ITaskRecordStore
{
    private readonly string _directory = Path.GetFullPath(directory);
    private readonly ITaskRecordCodec _codec = codec;

    /// <inheritdoc/>
    public TaskStoreReadResult ReadAll()
    {
        List<TaskRecord> records = [];
        List<TaskStoreFailure> failures = [];
        if (!Directory.Exists(_directory))
        {
            return new TaskStoreReadResult(records, failures);
        }

        string[] files;
        try
        {
            files = [.. Directory.EnumerateFiles(_directory, $"*{_codec.FileExtension}").OrderBy(Path.GetFileName, StringComparer.Ordinal)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            failures.Add(new TaskStoreFailure(_directory, [Error.Failure(FlowErrorCodes.RecordRead, $"读取存档目录失败：{ex.Message}")]));

            return new TaskStoreReadResult(records, failures);
        }

        foreach (string file in files)
        {
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failures.Add(new TaskStoreFailure(Path.GetFileName(file), [Error.Failure(FlowErrorCodes.RecordRead, $"读取存档失败：{ex.Message}")]));
                continue;
            }

            ErrorOr<TaskRecord> read = _codec.Read(text);
            if (read.IsError)
            {
                failures.Add(new TaskStoreFailure(Path.GetFileName(file), read.ErrorsOrEmptyList));
                continue;
            }

            records.Add(read.Value);
        }

        return new TaskStoreReadResult(records, failures);
    }

    /// <inheritdoc/>
    public ErrorOr<Success> Write(TaskRecord record)
    {
        ErrorOr<string> text = _codec.Write(record);
        if (text.IsError)
        {
            return text.ErrorsOrEmptyList;
        }

        string path = Path.Combine(_directory, $"{record.Identity.Id.Value}{_codec.FileExtension}");
        try
        {
            AtomicFile.WriteText(path, text.Value);

            return Result.Success;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [Error.Failure(FlowErrorCodes.RecordWrite, $"写入存档 {path} 失败：{ex.Message}")];
        }
    }

    /// <inheritdoc/>
    public ErrorOr<Success> Delete(TaskId id)
    {
        string path = Path.Combine(_directory, $"{id.Value}{_codec.FileExtension}");
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return Result.Success;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [Error.Failure(FlowErrorCodes.RecordWrite, $"删除存档 {path} 失败：{ex.Message}")];
        }
    }
}

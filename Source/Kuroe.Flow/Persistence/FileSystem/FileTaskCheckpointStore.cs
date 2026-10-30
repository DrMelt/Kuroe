using ErrorOr;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Persistence.FileSystem;

/// <summary>任务检查点的文件存储：每个任务一个 JSON 文件，写入走原子替换，读取逐文件报告失败。</summary>
public sealed class FileTaskCheckpointStore(string directory, ITaskCheckpointCodec codec) : ITaskCheckpointStore
{
    private readonly string _directory = Path.GetFullPath(directory);
    private readonly ITaskCheckpointCodec _codec = codec;

    /// <inheritdoc/>
    public TaskStoreReadResult ReadAll()
    {
        List<TaskCheckpointDocument> documents = [];
        List<TaskStoreFailure> failures = [];
        if (!Directory.Exists(_directory))
        {
            return new TaskStoreReadResult(documents, failures);
        }

        string[] files;
        try
        {
            files = [.. Directory.EnumerateFiles(_directory, "*.json").OrderBy(Path.GetFileName, StringComparer.Ordinal)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            failures.Add(new TaskStoreFailure(_directory, [Error.Failure(FlowErrorCodes.CheckpointRead, $"读取检查点目录失败：{ex.Message}")]));

            return new TaskStoreReadResult(documents, failures);
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
                failures.Add(new TaskStoreFailure(Path.GetFileName(file), [Error.Failure(FlowErrorCodes.CheckpointRead, $"读取检查点失败：{ex.Message}")]));
                continue;
            }

            ErrorOr<TaskCheckpointDocument> read = _codec.Read(text);
            if (read.IsError)
            {
                failures.Add(new TaskStoreFailure(Path.GetFileName(file), read.ErrorsOrEmptyList));
                continue;
            }

            documents.Add(read.Value);
        }

        return new TaskStoreReadResult(documents, failures);
    }

    /// <inheritdoc/>
    public ErrorOr<Success> Write(TaskCheckpointDocument document)
    {
        ErrorOr<string> text = _codec.Write(document);
        if (text.IsError)
        {
            return text.ErrorsOrEmptyList;
        }

        string path = Path.Combine(_directory, $"{document.Task.Identity.Id.Value}.json");
        try
        {
            AtomicFile.WriteText(path, text.Value);

            return Result.Success;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [Error.Failure(FlowErrorCodes.CheckpointWrite, $"写入检查点 {path} 失败：{ex.Message}")];
        }
    }

    /// <inheritdoc/>
    public ErrorOr<Success> Delete(TaskId id)
    {
        string path = Path.Combine(_directory, $"{id.Value}.json");
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
            return [Error.Failure(FlowErrorCodes.CheckpointWrite, $"删除检查点 {path} 失败：{ex.Message}")];
        }
    }
}

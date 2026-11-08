using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Shared;

namespace Kuroe.Workflows.Tasks;

/// <summary>任务工作目录：按任务键、流程与标题给目录命名，在启动目录下建立。</summary>
public sealed class TaskDirectory
{
    private readonly Lock _gate = new();
    private readonly string _root;

    private TaskDirectory(string root) => _root = root;

    /// <summary>任务目录的根。</summary>
    public string Root => _root;

    /// <summary>按根目录建立任务目录服务，目录不存在时按需建立。</summary>
    public static ErrorOr<TaskDirectory> Create(string directory)
    {
        try
        {
            return new TaskDirectory(Path.GetFullPath(directory));
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            return Error.Failure(ErrorCodes.TaskWorkspaceCreate, $"任务目录不是合法路径：{directory}");
        }
    }

    /// <summary>按名字建立任务目录并给出它的绝对路径。名字已被目录或文件占用时给出冲突错误。</summary>
    public ErrorOr<string> Prepare(TaskId id, FlowName flow, string title)
    {
        string path = Path.Combine(_root, TaskDirectoryName.Of(id, flow, title));

        lock (_gate)
        {
            try
            {
                if (Directory.Exists(path) || File.Exists(path))
                {
                    return Error.Conflict(ErrorCodes.TaskWorkspaceConflict, $"任务目录已存在：{path}");
                }

                Directory.CreateDirectory(path);

                return path;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return Error.Failure(ErrorCodes.TaskWorkspaceCreate, $"建立任务目录 {path} 失败：{ex.Message}");
            }
        }
    }

    /// <summary>按键前缀查找任务目录，未建立时为空。</summary>
    public string? Find(TaskId id)
    {
        lock (_gate)
        {
            try
            {
                return Directory.Exists(_root)
                    ? Directory.EnumerateDirectories(_root, $"{id.Value}-*").FirstOrDefault()
                    : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    /// <summary>删除任务目录，目录不存在时视为成功。</summary>
    public ErrorOr<Success> Remove(TaskId id)
    {
        lock (_gate)
        {
            try
            {
                if (!Directory.Exists(_root))
                {
                    return Result.Success;
                }

                foreach (string directory in Directory.EnumerateDirectories(_root, $"{id.Value}-*"))
                {
                    Directory.Delete(directory, recursive: true);
                }

                return Result.Success;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return Error.Failure(ErrorCodes.TaskWorkspaceCreate, $"删除任务目录 {id.Short} 失败：{ex.Message}");
            }
        }
    }

}

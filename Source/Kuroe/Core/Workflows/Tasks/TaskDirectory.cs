using ErrorOr;
using Kuroe.Flow.Node.RuntimeGraph.Execution;
using Kuroe.Shared;

namespace Kuroe.Workflows.Tasks;

/// <summary>任务工作目录：以任务键给目录命名，在启动目录下建立。</summary>
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

    /// <summary>按任务键建立任务目录并给出它的绝对路径。名字已被目录或文件占用时给出冲突错误。</summary>
    public ErrorOr<string> Prepare(TaskId id)
    {
        string path = Path.Combine(_root, id.Value);

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

    /// <summary>按任务键查找任务目录，未建立时为空。</summary>
    public string? Find(TaskId id)
    {
        lock (_gate)
        {
            try
            {
                string path = Path.Combine(_root, id.Value);

                return Directory.Exists(path) ? path : null;
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
                string path = Path.Combine(_root, id.Value);
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
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

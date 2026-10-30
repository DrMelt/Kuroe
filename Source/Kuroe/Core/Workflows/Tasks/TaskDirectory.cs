using System.Text;
using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Shared;

namespace Kuroe.Workflows.Tasks;

/// <summary>任务工作目录：按任务键、流程与标题给目录命名，在启动目录下建立。</summary>
public sealed class TaskDirectory
{
    /// <summary>流程段的字符上限。</summary>
    private const int FlowLimit = 20;

    /// <summary>标题段的字符上限。</summary>
    private const int TitleLimit = 40;

    /// <summary>任务目录完整路径的字符上限。</summary>
    private const int PathLimit = 240;

    /// <summary>文件系统不接受的名字字符。</summary>
    private static readonly char[] InvalidChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

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
        string path = Path.Combine(_root, Compose(id, flow, title));
        if (path.Length > PathLimit)
        {
            return Error.Validation(ErrorCodes.TaskWorkspaceCreate, $"任务目录路径过长：{path}");
        }

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

    /// <summary>任务目录名：任务键、流程与标题以连字符连接。</summary>
    private static string Compose(TaskId id, FlowName flow, string title) =>
        $"{id.Value}-{FlowSegment(flow.Value)}-{TitleSegment(title)}";

    /// <summary>流程段：连字符是段分隔符，段内出现时替换为下划线。</summary>
    private static string FlowSegment(string flow) => Clean(flow, FlowLimit, keepHyphen: false);

    /// <summary>标题段：位于名字末尾，连字符保留。</summary>
    private static string TitleSegment(string title) => Clean(title, TitleLimit, keepHyphen: true);

    /// <summary>替换非法字符与控制字符，去掉首尾空白与结尾点，按字符数截断，结果为空时用下划线占位。</summary>
    private static string Clean(string text, int limit, bool keepHyphen)
    {
        StringBuilder builder = new(Math.Min(text.Length, limit));
        foreach (Rune rune in text.EnumerateRunes())
        {
            builder.Append(IsInvalid(rune, keepHyphen) ? "_" : rune.ToString());
        }

        string cleaned = Truncate(builder.ToString().Trim(), limit).TrimEnd('.', ' ', '_');

        return cleaned.Length == 0 ? "_" : cleaned;
    }

    /// <summary>按字符数截断，不切断代理对。</summary>
    private static string Truncate(string text, int limit)
    {
        StringBuilder builder = new(limit);
        int count = 0;
        foreach (Rune rune in text.EnumerateRunes())
        {
            if (count == limit)
            {
                break;
            }

            builder.Append(rune.ToString());
            count++;
        }

        return builder.ToString();
    }

    /// <summary>字符是否要替换成下划线：控制字符、文件系统非法字符，以及流程段里的连字符。</summary>
    private static bool IsInvalid(Rune rune, bool keepHyphen) =>
        Rune.IsControl(rune)
        || (!keepHyphen && rune.Value == '-')
        || (rune.IsAscii && Array.IndexOf(InvalidChars, (char)rune.Value) >= 0);
}

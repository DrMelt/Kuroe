using System.Globalization;
using System.Text;
using ErrorOr;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Shared;

namespace Kuroe.Workflows.Tasks;

/// <summary>任务工作目录：按任务号、流程与标题给目录命名，在启动目录下建立，并按已有目录续号。</summary>
public sealed class TaskDirectory
{
    /// <summary>任务号的定宽位数。</summary>
    private const int NumberWidth = 4;

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
    private readonly int _nextTaskNumber;

    private TaskDirectory(string root, int nextTaskNumber)
    {
        _root = root;
        _nextTaskNumber = nextTaskNumber;
    }

    /// <summary>任务目录的根。</summary>
    public string Root => _root;

    /// <summary>首个可用任务号，按已有任务目录的最大号加一得出。</summary>
    public int NextTaskNumber => _nextTaskNumber;

    /// <summary>扫描已有任务目录给出续号，目录不存在时从 1 开始。</summary>
    public static ErrorOr<TaskDirectory> Create(string directory)
    {
        string root = Path.GetFullPath(directory);
        if (!Directory.Exists(root))
        {
            return new TaskDirectory(root, 1);
        }

        try
        {
            int next = 1;
            foreach (string entry in Directory.EnumerateDirectories(root))
            {
                if (TaskNumberAt(Path.GetFileName(entry)) is { } number && number >= next)
                {
                    next = number + 1;
                }
            }

            return new TaskDirectory(root, next);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Error.Failure(ErrorCodes.TaskWorkspaceCreate, $"扫描任务目录 {root} 失败：{ex.Message}");
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

    /// <summary>任务目录名：任务号、流程与标题以连字符连接。</summary>
    private static string Compose(TaskId id, FlowName flow, string title)
    {
        string number = id.Value.ToString($"D{NumberWidth}", CultureInfo.InvariantCulture);

        return $"{number}-{FlowSegment(flow.Value)}-{TitleSegment(title)}";
    }

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

    /// <summary>目录名开头的任务号；名字不是四位以上数字加连字符时返回 null。</summary>
    private static int? TaskNumberAt(string name)
    {
        int separator = name.IndexOf('-');
        if (separator < NumberWidth
            || !int.TryParse(name.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out int number))
        {
            return null;
        }

        return number;
    }
}
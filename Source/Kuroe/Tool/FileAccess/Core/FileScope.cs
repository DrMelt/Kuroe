using System.Globalization;
using System.Text;
using ErrorOr;

namespace Kuroe.Tool.FileAccess;

/// <summary>根目录内的文件访问：路径解析与越界拒绝，按行号范围读取并按字符上限截断。
/// 不解析链接，根目录内指向外部的符号链接、目录联接点与硬链接不被拦截。</summary>
/// <param name="root">路径参数的解析基准目录，绝对路径。</param>
/// <param name="boundary">允许的边界目录，绝对路径。</param>
/// <param name="rootName">基准目录名，空路径的展示名由它拼出。</param>
/// <param name="boundaryName">边界目录名，越界拒绝文案由它拼出。</param>
public sealed class FileScope(string root, string boundary, string rootName, string boundaryName)
{
    /// <summary>单条读取内容与提示文本的字符上限，超长截断并提示。</summary>
    private const int OutputLimit = 8000;

    private readonly string _root = root;
    private readonly string _rootName = rootName;

    /// <summary>路径解析基准与边界相同的根目录。</summary>
    public FileScope(string root, string name)
        : this(root, root, name, name)
    {
    }

    /// <summary>根目录的绝对路径。</summary>
    public string Root => _root;

    /// <summary>建立作用域时的绑定错误，非空时所有解析都返回它。</summary>
    public Error? BindingError { get; init; }

    /// <summary>路径参数的解析范围，解析基准是根目录，边界是边界目录。</summary>
    public ToolPathScope Paths { get; } = new(root, boundary, boundaryName);

    /// <summary>把相对路径解析为根目录内的绝对路径，越界或不可解析时给出拒绝错误。</summary>
    public ErrorOr<string> Resolve(string path) =>
        BindingError is { } error ? error : Paths.Resolve(path);

    /// <summary>列出根目录内目录的直接子项，按名字序数排序。目标是文件或不存在时给出拒绝错误。
    /// fileFunction 是提示里指出的读取函数名。</summary>
    public ErrorOr<IReadOnlyList<DirectoryEntry>> ListEntries(string path, string fileFunction)
    {
        ErrorOr<string> resolved = Resolve(path);
        if (resolved.IsError)
        {
            return resolved.ErrorsOrEmptyList;
        }

        if (File.Exists(resolved.Value))
        {
            return ToolErrors.Path($"{Display(path)} 是文件，用 {fileFunction} 读取它的内容。");
        }

        if (!Directory.Exists(resolved.Value))
        {
            return ToolErrors.Path($"{Display(path)} 不存在。");
        }

        try
        {
            return Directory.GetFileSystemEntries(resolved.Value)
                .Select(entry => new DirectoryEntry(Path.GetFileName(entry), Directory.Exists(entry)))
                .OrderBy(entry => entry.Name, StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.ReadFailed($"列目录 {Display(path)} 失败：{ex.Message}");
        }
    }

    /// <summary>读取根目录内文件指定行号范围的内容，每行带行号并按字符上限截断。
    /// 行号从 1 起，结束行超出文件长度时读到文件尾并在正文前注明实际范围。listFunction 是提示里指出的列目录函数名。</summary>
    public ErrorOr<string> ReadRange(string path, int from, int to, string listFunction)
    {
        ErrorOr<string> file = ExistingFile(path, listFunction);
        return file.IsError ? file.ErrorsOrEmptyList : ReadLines(file.Value, path, from, to);
    }

    /// <summary>取根目录内文件的文件系统事实，只受理文件。listFunction 是提示里指出的列目录函数名。</summary>
    public ErrorOr<FileDetails> Inspect(string path, string listFunction)
    {
        ErrorOr<string> file = ExistingFile(path, listFunction);
        if (file.IsError)
        {
            return file.ErrorsOrEmptyList;
        }

        try
        {
            FileInfo info = new(file.Value);

            return new FileDetails(
                info.Length,
                new DateTimeOffset(info.CreationTime),
                new DateTimeOffset(info.LastWriteTime),
                new DateTimeOffset(info.LastAccessTime),
                info.Attributes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.ReadFailed($"读取 {path} 信息失败：{ex.Message}");
        }
    }

    /// <summary>解析到根目录内已存在的文件。目标是目录或不存在时给出拒绝错误。</summary>
    private ErrorOr<string> ExistingFile(string path, string listFunction)
    {
        ErrorOr<string> resolved = Resolve(path);
        if (resolved.IsError)
        {
            return resolved.ErrorsOrEmptyList;
        }

        if (Directory.Exists(resolved.Value))
        {
            return ToolErrors.Path($"{path} 是目录，用 {listFunction} 列出它的条目。");
        }

        if (!File.Exists(resolved.Value))
        {
            return ToolErrors.Path($"{path} 不存在。");
        }

        return resolved.Value;
    }

    /// <summary>读取文件指定行号范围的内容，每行带行号并按字符上限截断。
    /// 行号从 1 起，结束行超出文件长度时读到文件尾并在正文前注明实际范围。</summary>
    private static ErrorOr<string> ReadLines(string fullPath, string path, int from, int to)
    {
        try
        {
            using StreamReader reader = new(fullPath);

            for (int line = 1; line < from; line++)
            {
                if (reader.ReadLine() is null)
                {
                    return ToolErrors.Argument($"起始行 {from} 超出文件长度，该范围没有内容。");
                }
            }

            int maxLines = to - from + 1;
            List<string> lines = [];
            bool reachedEnd = false;
            int rawLength = 0;
            while (lines.Count < maxLines)
            {
                string? line = reader.ReadLine();
                if (line is null)
                {
                    reachedEnd = true;
                    break;
                }

                lines.Add(line);
                rawLength += line.Length + 1;
                if (rawLength > OutputLimit)
                {
                    break;
                }
            }

            if (lines.Count == 0)
            {
                return "（已到文件结尾，文件没有内容）";
            }

            string content = Numbered(lines, from, out bool limited);
            if (limited)
            {
                return $"（内容超过 {OutputLimit} 字符，已截断）\n{content}";
            }

            if (!reachedEnd)
            {
                return content;
            }

            int last = from + lines.Count - 1;
            string range = from == last ? $"第 {from} 行" : $"第 {from} 到 {last} 行";
            return $"（已到文件结尾，实际返回{range}）\n{content}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.ReadFailed($"读取 {path} 失败：{ex.Message}");
        }
    }

    /// <summary>把行按行号编号，行号右对齐，正文超过字符上限时截断并标记。</summary>
    private static string Numbered(List<string> lines, int from, out bool limited)
    {
        int width = (from + lines.Count - 1).ToString(CultureInfo.InvariantCulture).Length;
        var text = new StringBuilder();
        limited = false;
        for (int index = 0; index < lines.Count; index++)
        {
            if (text.Length > 0)
            {
                text.Append('\n');
            }

            text.Append((from + index).ToString(CultureInfo.InvariantCulture).PadLeft(width));
            text.Append(" | ");
            text.Append(lines[index]);
            if (text.Length > OutputLimit)
            {
                limited = true;
                break;
            }
        }

        string content = text.ToString();
        return content.Length <= OutputLimit ? content : content[..OutputLimit];
    }

    /// <summary>按字符上限截断文本，超长保留前限并在末尾加截断标记。</summary>
    public static string Truncate(string text) =>
        text.Length <= OutputLimit ? text : string.Concat(text.AsSpan(0, OutputLimit), "…内容已截断");

    /// <summary>路径的展示。空路径即路径基准目录。</summary>
    public string Display(string path) => path.Length == 0 ? $"{_rootName}根" : path;
}

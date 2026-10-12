using System.Text;
using ErrorOr;
using Kuroe.Shared.Executions.Tools;

namespace Kuroe.Tools.Files;

/// <summary>根目录内的文件访问：路径解析与越界拒绝，按行号范围读取并按字符上限截断。
/// 不解析链接，根目录内指向外部的链接不被拦截。</summary>
internal sealed class FileScope
{
    /// <summary>单条读取内容与提示文本的字符上限，超长截断并提示。</summary>
    private const int OutputLimit = 8000;

    private readonly string _root;
    private readonly string _name;

    /// <param name="root">根目录的绝对路径。</param>
    /// <param name="name">根目录名，错误文案与空路径的展示名都由它拼出。</param>
    internal FileScope(string root, string name)
    {
        _root = root;
        _name = name;
    }

    /// <summary>根目录的绝对路径。</summary>
    internal string Root => _root;

    /// <summary>把相对路径解析为根目录内的绝对路径，越界或不可解析时给出拒绝错误。</summary>
    internal ErrorOr<string> Resolve(string path)
    {
        string full;
        try
        {
            full = Path.GetFullPath(path, _root);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            return ToolErrors.Path($"路径 {Display(path)} 无法解析：{ex.Message}");
        }

        string relative = Path.GetRelativePath(_root, full);
        if (Path.IsPathRooted(relative) || IsOutside(relative))
        {
            return ToolErrors.Path($"路径 {Display(path)} 不在{_name}内。");
        }

        return full;
    }

    /// <summary>相对路径是否落在根目录之外。首段是 .. 即越界，..foo 这类是根目录内的名字。</summary>
    private static bool IsOutside(string relative) =>
        relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    /// <summary>读取根目录内文件指定行号范围的内容，按行截取并按字符上限截断。
    /// 行号从 1 起，结束行超出文件长度时读到文件尾并附实际行数。listFunction 是提示里指出的列目录函数名。</summary>
    internal ErrorOr<string> ReadRange(string path, int from, int to, string listFunction)
    {
        ErrorOr<string> file = ExistingFile(path, listFunction);
        return file.IsError ? file.ErrorsOrEmptyList : ReadLines(file.Value, path, from, to);
    }

    /// <summary>读取根目录内整个文件的内容，按字符上限截断。listFunction 是提示里指出的列目录函数名。</summary>
    internal ErrorOr<string> ReadWhole(string path, string listFunction)
    {
        ErrorOr<string> file = ExistingFile(path, listFunction);
        return file.IsError ? file.ErrorsOrEmptyList : ReadAll(file.Value, path);
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

    /// <summary>读取文件指定行号范围的内容，按行截取并按字符上限截断。
    /// 行号从 1 起，结束行超出文件长度时读到文件尾并附实际行数。</summary>
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
            var text = new StringBuilder();
            int readLines = 0;
            bool limited = false;
            while (readLines < maxLines && !limited)
            {
                string? line = reader.ReadLine();
                if (line is null)
                {
                    break;
                }

                readLines++;
                if (text.Length > 0)
                {
                    text.Append('\n');
                }

                text.Append(line);
                if (text.Length > OutputLimit)
                {
                    limited = true;
                }
            }

            string content = Truncate(text.ToString());
            if (!limited && readLines < maxLines)
            {
                content += $"（已到文件结尾，实际返回 {readLines} 行）";
            }

            return content;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.ReadFailed($"读取 {path} 失败：{ex.Message}");
        }
    }

    /// <summary>读取整个文件，按字符上限截断。</summary>
    private static ErrorOr<string> ReadAll(string fullPath, string path)
    {
        try
        {
            using StreamReader reader = new(fullPath);
            char[] buffer = new char[OutputLimit + 1];
            int offset = 0;
            while (offset < buffer.Length)
            {
                int read = reader.Read(buffer, offset, buffer.Length - offset);
                if (read == 0)
                {
                    break;
                }

                offset += read;
            }

            return Truncate(new string(buffer, 0, offset));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.ReadFailed($"读取 {path} 失败：{ex.Message}");
        }
    }

    /// <summary>按字符上限截断文本，超长保留前限并在末尾加截断标记。</summary>
    internal static string Truncate(string text) =>
        text.Length <= OutputLimit ? text : string.Concat(text.AsSpan(0, OutputLimit), "…内容已截断");

    /// <summary>路径的展示。空路径即根目录。</summary>
    internal string Display(string path) => path.Length == 0 ? $"{_name}根" : path;
}

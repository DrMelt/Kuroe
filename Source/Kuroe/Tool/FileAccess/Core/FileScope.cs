using ErrorOr;
using Kuroe.Files.Shared;
using Kuroe.Tool.FileAccess.Shared;

namespace Kuroe.Tool.FileAccess;

/// <summary>根目录内的文件访问：路径解析与越界拒绝，按行号范围读取并保留每行终止符。
/// 不解析链接，根目录内指向外部的符号链接、目录联接点与硬链接不被拦截。</summary>
/// <param name="root">路径参数的解析基准目录，绝对路径。</param>
/// <param name="boundary">允许的边界目录，绝对路径。</param>
/// <param name="rootName">基准目录名，空路径的展示名由它拼出。</param>
/// <param name="boundaryName">边界目录名，越界拒绝文案由它拼出。</param>
/// <param name="files">文件访问实现。</param>
public sealed class FileScope(
    string root,
    string boundary,
    string rootName,
    string boundaryName,
    IWorkspaceFiles files) : IFileScope
{
    private readonly string _root = root;
    private readonly string _rootName = rootName;
    private readonly IWorkspaceFiles _files = files;

    /// <summary>路径解析基准与边界相同的根目录。</summary>
    public FileScope(string root, string name, IWorkspaceFiles files)
        : this(root, root, name, name, files)
    {
    }

    /// <summary>根目录的绝对路径。</summary>
    public string Root => _root;

    /// <summary>本作用域使用的文件访问实现。</summary>
    internal IWorkspaceFiles Workspace => _files;

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

        if (_files.FileExists(resolved.Value))
        {
            return ToolErrors.Path($"{Display(path)} 是文件，用 {fileFunction} 读取它的内容。");
        }

        if (!_files.DirectoryExists(resolved.Value))
        {
            return ToolErrors.Path($"{Display(path)} 不存在。");
        }

        try
        {
            return _files.Entries(resolved.Value)
                .Select(entry => new DirectoryEntry(Path.GetFileName(entry), _files.DirectoryExists(entry)))
                .OrderBy(entry => entry.Name, StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.ReadFailed($"列目录 {Display(path)} 失败：{ex.Message}");
        }
    }

    /// <summary>读取根目录内文件指定行号范围的内容，保留行尾终止符。
    /// 行号从 1 起，结束行超出文件长度时读到文件尾，起始行超出文件长度时给出拒绝错误。
    /// characterBudget 是行内容与终止符的字符预算。listFunction 是提示里指出的列目录函数名。</summary>
    public ErrorOr<SourceLineReadResult> ReadRange(
        string path,
        int from,
        int to,
        int characterBudget,
        string listFunction)
    {
        if (from < 1)
        {
            return ToolErrors.Argument("起始行号 from 从 1 起。");
        }

        if (to < from)
        {
            return ToolErrors.Argument("结束行号 to 不能小于起始行号 from。");
        }

        if (characterBudget < 1)
        {
            return ToolErrors.Argument("字符预算必须大于 0。");
        }

        ErrorOr<string> file = ResolveFile(path, listFunction);
        return file.IsError
            ? file.ErrorsOrEmptyList
            : ReadLines(file.Value, path, from, to, characterBudget);
    }

    /// <summary>解析到根目录内已存在的文件。目标是目录或不存在时给出拒绝错误。listFunction 是提示里指出的列目录函数名。</summary>
    public ErrorOr<string> ResolveFile(string path, string listFunction)
    {
        ErrorOr<string> resolved = Resolve(path);
        if (resolved.IsError)
        {
            return resolved.ErrorsOrEmptyList;
        }

        if (_files.DirectoryExists(resolved.Value))
        {
            return ToolErrors.Path($"{path} 是目录，用 {listFunction} 列出它的条目。");
        }

        if (!_files.FileExists(resolved.Value))
        {
            return ToolErrors.Path($"{path} 不存在。");
        }

        return resolved.Value;
    }

    /// <summary>读取文件指定行号范围，保留每行终止符。起始行超出文件长度时给出拒绝错误。</summary>
    private ErrorOr<SourceLineReadResult> ReadLines(
        string fullPath,
        string path,
        int from,
        int to,
        int characterBudget)
    {
        try
        {
            using StreamReader reader = new(_files.OpenRead(fullPath));

            SourceLineReadResult read = SourceLineReader.Read(reader, from, to, characterBudget);
            if (read.Lines.Count == 0 && read.ToBeyondEnd && from > 1)
            {
                return ToolErrors.Argument($"起始行 {from} 超出文件长度，该范围没有内容。");
            }

            return read;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.ReadFailed($"读取 {path} 失败：{ex.Message}");
        }
    }

    /// <summary>路径的展示。空路径即路径基准目录。</summary>
    public string Display(string path) => path.Length == 0 ? $"{_rootName}根" : path;
}

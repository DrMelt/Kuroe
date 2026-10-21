using System.Text;
using ErrorOr;
using Kuroe.Tool.FileAccess;

namespace Kuroe.Tool.File;

/// <summary>列出工作目录内目录条目的工具。路径解析后必须落回工作目录内。</summary>
public sealed class ListDirectoryTool : ITool
{
    /// <summary>目录列表的条目上限，超出截断并提示。</summary>
    private const int MaxEntries = 200;

    private readonly FileScope _scope;

    /// <summary>列出目录的直接子项。</summary>
    private static readonly ToolDefinition ListDirectory = new(
        new ToolPath("file/read/ListDirectory"),
        "列出工作目录内一个目录的直接子项。path 缺省时列出工作目录根的条目。",
        [new ToolParameter(new ToolName("path"), "相对工作目录的目录路径，缺省为工作目录根")]);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>用工作目录建立载体。</summary>
    public ListDirectoryTool(string baseDirectory)
    {
        _scope = FileTools.Scope(baseDirectory);
        Functions = [ListDirectory.Bind(List)];
    }

    /// <summary>列出目录的直接子项，按名字排序并标注目录；错误与拒绝以 ErrorOr 表达。</summary>
    private ErrorOr<string> List(ToolArguments arguments)
    {
        string? path = arguments.Text(new ToolName("path"));
        path ??= string.Empty;

        ErrorOr<IReadOnlyList<DirectoryEntry>> entries = _scope.ListEntries(path, "ReadFile");
        if (entries.IsError)
        {
            return entries.ErrorsOrEmptyList;
        }

        if (entries.Value.Count == 0)
        {
            return $"目录 {_scope.Display(path)} 为空。";
        }

        var text = new StringBuilder();
        text.AppendLine($"{_scope.Display(path)} 的条目（{entries.Value.Count}）：");
        foreach (DirectoryEntry entry in entries.Value.Take(MaxEntries))
        {
            text.AppendLine($"  {entry.Name}{(entry.IsDirectory ? "/" : string.Empty)}");
        }

        if (entries.Value.Count > MaxEntries)
        {
            text.Append($"…仅列出前 {MaxEntries} 条");
        }

        return text.ToString().TrimEnd();
    }
}
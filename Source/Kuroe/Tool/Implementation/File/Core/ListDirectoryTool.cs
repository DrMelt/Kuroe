using System.Text;
using ErrorOr;
using Kuroe.Shared;
using Kuroe.Tool.FileAccess;

namespace Kuroe.Tool.File;

/// <summary>列出路径基准目录内目录条目的工具。路径解析后必须落回工作区内。</summary>
public sealed class ListDirectoryTool : ITool
{
    /// <summary>目录列表的条目上限，超出截断并提示。</summary>
    private const int MaxEntries = 200;

    private readonly FileScope _scope;

    /// <summary>列出目录的直接子项。</summary>
    public static ToolDefinition Definition { get; } = new(
        new ToolPath("file/read/ListDirectory"),
        "列出路径基准目录内一个目录的直接子项。path 缺省时列出路径基准目录的条目。",
        [new ToolParameter(new ToolName("path"), "相对路径基准目录的目录路径，缺省为路径基准目录", Path: true, PathAccess: FileTools.PathAccess)],
        [PathVariables.RootName]);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>用工作目录建立载体。</summary>
    public ListDirectoryTool(string baseDirectory)
        : this(ToolBinding.Root(baseDirectory))
    {
    }

    /// <summary>用执行绑定建立载体，路径变量由工具自行读取。</summary>
    public ListDirectoryTool(ToolBinding binding)
    {
        _scope = FileTools.Scope(binding);
        Functions = [Definition.Bind(List, _scope.Paths)];
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
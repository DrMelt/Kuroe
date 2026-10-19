using System.Text;
using ErrorOr;
using Kuroe.Tool.FileAccess;

namespace Kuroe.Tool.File;

/// <summary>一次读取工作目录内多个文本文件的工具。路径解析后必须落回工作目录内，单个条目的错误不阻断其它条目。</summary>
public sealed class ReadFilesTool : ITool
{
    /// <summary>批量读取一次交回的总字符上限，超出后停止并提示。</summary>
    private const int TotalLimit = 40000;

    private readonly FileScope _scope;

    /// <summary>一次读取多个文件。</summary>
    private static readonly ToolDefinition ReadFiles = new(
        new ToolPath("file/read/ReadFiles"),
        "一次读取工作目录内的多个文本文件。paths 是相对工作目录的文件路径列表，结果按列表顺序分段交回。",
        [new ToolParameter(new ToolName("paths"), "相对工作目录的文件路径列表", Required: true, List: true)]);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>用工作目录建立载体。</summary>
    public ReadFilesTool(string baseDirectory)
    {
        _scope = FileTools.Scope(baseDirectory);
        Functions = [ReadFiles.Bind(Read)];
    }

    /// <summary>一次读取多个文件，结果按传入顺序分段交回；整体超长截断。单个条目的错误不阻断其它条目。</summary>
    private ErrorOr<string> Read(ToolArguments arguments)
    {
        IReadOnlyList<string>? paths = arguments.List(new ToolName("paths"));
        if (paths is null || paths.Count == 0)
        {
            return ToolErrors.Argument("至少给一个文件路径。");
        }

        var text = new StringBuilder();
        foreach (string path in paths)
        {
            string section = $"=== {path} ===\n{ToolResult.Render(_scope.ReadWhole(path, "ListDirectory"))}\n";
            if (text.Length + section.Length > TotalLimit)
            {
                string marker = $"…批量读取已截断，当前交回 {text.Length} 字符";
                if (text.Length + marker.Length > TotalLimit)
                {
                    text.Length = TotalLimit - marker.Length;
                }

                text.Append(marker);
                return text.ToString();
            }

            text.Append(section);
        }

        return text.ToString().TrimEnd();
    }
}
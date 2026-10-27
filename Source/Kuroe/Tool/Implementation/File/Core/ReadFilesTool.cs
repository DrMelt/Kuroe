using System.Text;
using ErrorOr;
using Kuroe.Shared;
using Kuroe.Tool.FileAccess;

namespace Kuroe.Tool.File;

/// <summary>一次读取路径基准目录内多个文本文件的工具。路径参数在调用前整体校验，文件不存在等读取错误逐项交回。</summary>
public sealed class ReadFilesTool : ITool
{
    /// <summary>批量读取一次交回的总字符上限，超出后停止并提示。</summary>
    private const int TotalLimit = 40000;

    private readonly FileScope _scope;

    /// <summary>一次读取多个文件。</summary>
    public static ToolDefinition Definition { get; } = new(
        new ToolPath("file/read/ReadFiles"),
        "一次读取路径基准目录内的多个文本文件。paths 是相对路径基准目录的文件路径列表，结果按列表顺序分段交回。",
        [new ToolParameter(new ToolName("paths"), "相对路径基准目录的文件路径列表", Required: true, List: true, Path: true, PathAccess: FileTools.PathAccess)],
        [PathVariables.RootName]);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>用工作目录建立载体。</summary>
    public ReadFilesTool(string baseDirectory)
        : this(ToolBinding.Root(baseDirectory))
    {
    }

    /// <summary>用执行绑定建立载体，路径变量由工具自行读取。</summary>
    public ReadFilesTool(ToolBinding binding)
    {
        _scope = FileTools.Scope(binding);
        Functions = [Definition.Bind(Read, _scope.Paths)];
    }

    /// <summary>一次读取多个文件，结果按传入顺序分段交回；整体超长截断。文件不存在等读取错误不阻断其它条目。</summary>
    private ErrorOr<string> Read(ToolArguments arguments)
    {
        IReadOnlyList<string> paths = arguments.List(new ToolName("paths"))!;

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
using ErrorOr;
using Kuroe.Tool.FileAccess;

namespace Kuroe.Tool.File;

/// <summary>取工作目录内文件的文件系统信息的工具。路径解析后必须落回工作目录内，只受理文件。</summary>
public sealed class GetFileInfoTool : ITool
{
    private readonly FileScope _scope;

    /// <summary>取一个文件的文件系统信息。</summary>
    private static readonly ToolDefinition GetFileInfo = new(
        new ToolPath("file/read/GetFileInfo"),
        "取工作目录内一个文件的文件系统信息：字节大小、创建/修改/访问时间、只读隐藏系统归档属性。只报文件系统事实，不判断内容；目录用 ListDirectory 列出条目。",
        [new ToolParameter(new ToolName("path"), "相对工作目录的文件路径", Required: true)]);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>用工作目录建立载体。</summary>
    public GetFileInfoTool(string baseDirectory)
    {
        _scope = FileTools.Scope(baseDirectory);
        Functions = [GetFileInfo.Bind(Info)];
    }

    /// <summary>取一个文件的文件系统事实并渲染成文本，错误与拒绝以 ErrorOr 表达。目录不受理，提示改用 ListDirectory。</summary>
    private ErrorOr<string> Info(ToolArguments arguments)
    {
        string? path = arguments.Text(new ToolName("path"));
        if (string.IsNullOrWhiteSpace(path))
        {
            return ToolErrors.Argument("缺少参数 path。");
        }

        ErrorOr<FileDetails> details = _scope.Inspect(path, "ListDirectory");
        if (details.IsError)
        {
            return details.ErrorsOrEmptyList;
        }

        return string.Join('\n',
            path,
            $"大小：{details.Value.Size} 字节",
            $"创建时间：{details.Value.Created:yyyy-MM-dd HH:mm:ss}",
            $"修改时间：{details.Value.LastWrite:yyyy-MM-dd HH:mm:ss}",
            $"访问时间：{details.Value.LastAccess:yyyy-MM-dd HH:mm:ss}",
            $"属性：{Describe(details.Value.Attributes)}");
    }

    /// <summary>属性位里的四项映射成中文名并以顿号连接，四项都为假时为「无」。</summary>
    private static string Describe(FileAttributes attributes)
    {
        List<string> names = [];
        if (attributes.HasFlag(FileAttributes.ReadOnly))
        {
            names.Add("只读");
        }

        if (attributes.HasFlag(FileAttributes.Hidden))
        {
            names.Add("隐藏");
        }

        if (attributes.HasFlag(FileAttributes.System))
        {
            names.Add("系统");
        }

        if (attributes.HasFlag(FileAttributes.Archive))
        {
            names.Add("归档");
        }

        return names.Count == 0 ? "无" : string.Join('、', names);
    }
}
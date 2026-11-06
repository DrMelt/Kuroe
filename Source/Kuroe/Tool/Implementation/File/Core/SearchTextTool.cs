using System.Text;
using ErrorOr;
using Kuroe.Shared;
using Kuroe.Tool.FileAccess;

namespace Kuroe.Tool.File;

/// <summary>在路径基准目录内按字面文本搜索文件内容的工具。路径解析后必须落回工作区内。</summary>
public sealed class SearchTextTool : ITool
{
    private readonly FileScope _scope;
    private readonly FileSearch _search;

    /// <summary>在路径范围内按字面文本搜索文件内容。</summary>
    public static ToolDefinition Definition { get; } = new(
        new ToolPath("file/read/SearchText"),
        "在路径基准目录内按字面文本搜索文件内容，按文件分组返回相对路径与带行号的整行内容。paths 限定搜索的目录或文件，省略时搜索整个路径基准目录；extensions 限定后缀名，可写 cs 或 .cs，省略时不限制；ignoreCase 为真时忽略大小写。递归搜索跳过工作区根下的 .kuroe 与 .git，跳过二进制文件，单行最多读取前 8000 字符并加省略号，输出超过 8000 字符时截断。",
        [
            new ToolParameter(new ToolName("pattern"), "要搜索的字面文本，不按正则解释，空或空白拒绝", Required: true),
            new ToolParameter(new ToolName("paths"), "相对路径基准目录的目录或文件路径列表，省略时搜索整个路径基准目录", List: true, Path: true, PathAccess: FileTools.PathAccess),
            new ToolParameter(new ToolName("extensions"), "限定文件后缀名，例如 cs 或 .cs，省略时不限制", List: true),
            new ToolParameter(new ToolName("ignoreCase"), "匹配是否忽略大小写，布尔，缺省为假", Flag: true),
        ], [PathVariables.RootName]);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>用工作目录建立载体。</summary>
    public SearchTextTool(string baseDirectory)
        : this(ToolBinding.Root(baseDirectory))
    {
    }

    /// <summary>用执行绑定建立载体，路径变量由工具自行读取。</summary>
    public SearchTextTool(ToolBinding binding)
    {
        _scope = FileTools.Scope(binding);
        _search = new FileSearch(_scope, FileTools.PathAccess);
        Functions = [Definition.Bind(Search, _scope.Paths)];
    }

    /// <summary>按范围、后缀与文本搜索，错误与拒绝以 ErrorOr 表达。</summary>
    private ErrorOr<string> Search(ToolArguments arguments)
    {
        ErrorOr<TextQuery> query = TextQuery.Create(
            arguments.Text(new ToolName("pattern")),
            arguments.List(new ToolName("extensions")),
            arguments.Flag(new ToolName("ignoreCase")) == true);
        if (query.IsError)
        {
            return query.ErrorsOrEmptyList;
        }

        IReadOnlyList<string> paths = arguments.List(new ToolName("paths")) ?? [];

        ErrorOr<TextSearchResult> result = _search.Search(paths, query.Value, FileScope.OutputLimit);
        if (result.IsError)
        {
            return result.ErrorsOrEmptyList;
        }

        return Render(result.Value, query.Value, paths);
    }

    /// <summary>把搜索结果渲染成按文件分组的带行号文本，整体超过输出上限时截断并提示。</summary>
    private string Render(TextSearchResult result, TextQuery query, IReadOnlyList<string> paths)
    {
        string scope = paths.Count == 0 ? _scope.Display(string.Empty) : string.Join('、', paths);
        if (result.Matches.Count == 0)
        {
            return NoMatches(result, query, scope);
        }

        string skipped = result.SkippedFiles == 0 ? string.Empty : $"，跳过 {result.SkippedFiles} 个文件";
        var text = new StringBuilder();
        text.Append($"在 {scope} 中匹配 {result.Matches.Count} 处{skipped}：\n");

        foreach (IGrouping<string, TextSearchMatch> group in result.Matches.GroupBy(match => match.File))
        {
            text.Append(DisplayPath(group.Key));
            text.Append('\n');
            text.Append(FileScope.FormatLines(
                [.. group.Select(match => new NumberedLine(match.Line, match.Text))]));
            text.Append('\n');

            if (text.Length > FileScope.OutputLimit)
            {
                break;
            }
        }

        return FileScope.Limit(text.ToString().TrimEnd('\n'), out _);
    }

    /// <summary>没有匹配时的交回文本，报告扫描与跳过的文件数。</summary>
    private static string NoMatches(TextSearchResult result, TextQuery query, string scope)
    {
        string skipped = result.SkippedFiles == 0 ? string.Empty : $"，跳过 {result.SkippedFiles} 个文件";
        return result.ScannedFiles == 0
            ? $"在 {scope} 中没有可搜索的文件。"
            : $"在 {scope} 中搜索 {query.Pattern} 没有匹配，已扫描 {result.ScannedFiles} 个文件{skipped}。";
    }

    /// <summary>文件的展示路径，相对路径基准目录。</summary>
    private string DisplayPath(string file) => Path.GetRelativePath(_scope.Root, file);
}
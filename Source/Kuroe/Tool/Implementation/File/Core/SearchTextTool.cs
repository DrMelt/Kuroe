using System.Text;
using ErrorOr;
using Kuroe.Shared;
using Kuroe.Files.Shared;
using Kuroe.Tool.FileAccess;

namespace Kuroe.Tool.File;

/// <summary>在路径基准目录内按字面文本搜索文件内容的工具。路径解析后必须落回工作区内。</summary>
public sealed class SearchTextTool : ITool
{
    /// <summary>判定不唯一需要观察到的出现次数，第二次出现即停止文件内容扫描。</summary>
    private const int DuplicateThreshold = 2;

    private readonly FileScope _scope;
    private readonly FileSearch _search;

    /// <summary>在路径范围内按字面文本搜索文件内容。</summary>
    public static ToolDefinition Definition { get; } = new(
        new ToolPath("file/read/SearchText"),
        "在路径基准目录内按字面文本搜索文件内容，按文件分组返回相对路径与带行号的匹配行。递归搜索跳过工作区根下的 .kuroe 与 .git，跳过二进制文件，匹配整行，同一行多处只展示一行，输出单行保留包含命中位置的 1000 字符窗口，被截断时在保留内容后标注该行完整字符数与展示的字符区间，输出超过 8000 字符时截断。",
        [
            new ToolParameter(new ToolName("pattern"), "要搜索的字面文本，不按正则解释，空或空白拒绝", Required: true),
            new ToolParameter(new ToolName("paths"), "相对路径基准目录的目录或文件路径列表，省略时搜索整个路径基准目录", List: true, Path: true, PathAccess: FileTools.PathAccess),
            new ToolParameter(new ToolName("extensions"), "限定文件后缀名，例如 cs 或 .cs，省略时不限制", List: true),
            new ToolParameter(new ToolName("ignoreCase"), "匹配是否忽略大小写，布尔，缺省为假", Flag: true),
            new ToolParameter(new ToolName("unique"), "要求 pattern 在范围内恰好出现 1 次，同一行多处按多次计，出现 0 次或多次时调用失败，多次时列出已定位的匹配行，布尔，缺省为假", Flag: true),
        ], [PathVariables.RootName]);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>用工作目录与文件访问实现建立载体。</summary>
    public SearchTextTool(string baseDirectory, IWorkspaceFiles files)
        : this(ToolBinding.Root(baseDirectory), files)
    {
    }

    /// <summary>用执行绑定与文件访问实现建立载体，路径变量由工具自行读取。</summary>
    public SearchTextTool(ToolBinding binding, IWorkspaceFiles files)
    {
        _scope = FileTools.Scope(binding, files);
        _search = new FileSearch(_scope, FileTools.PathAccess);
        Functions = [Definition.Bind(Search, _scope.Paths)];
    }

    /// <summary>按范围、后缀与文本搜索，错误与拒绝以 ErrorOr 表达。unique 为真时要求恰好出现 1 次。</summary>
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
        string scope = Scope(paths);
        bool unique = arguments.Flag(new ToolName("unique")) == true;

        ErrorOr<TextSearchResult> result = unique
            ? _search.Search(paths, query.Value, characterBudget: int.MaxValue, occurrenceLimit: DuplicateThreshold)
            : _search.Search(paths, query.Value, TextLimit.Characters);
        if (result.IsError)
        {
            return result.ErrorsOrEmptyList;
        }

        if (unique)
        {
            if (result.Value.ScannedFiles == 0)
            {
                return ToolErrors.Argument(NoSearchableFiles(scope));
            }

            if (result.Value.Occurrences == 0)
            {
                return ToolErrors.Argument(NoUniqueMatch(query.Value, scope));
            }

            if (result.Value.Occurrences > 1)
            {
                return ToolErrors.Argument(Duplicate(result.Value, query.Value, scope));
            }
        }

        return Render(result.Value, query.Value, scope);
    }

    /// <summary>搜索范围的展示文本，省略路径时取路径基准。</summary>
    private string Scope(IReadOnlyList<string> paths) =>
        paths.Count == 0 ? _scope.Display(string.Empty) : string.Join('、', paths);

    /// <summary>把搜索结果渲染成按文件分组的带行号文本，整体超过输出上限时截断并提示。</summary>
    private string Render(TextSearchResult result, TextQuery query, string scope)
    {
        if (result.Matches.Count == 0)
        {
            return result.ScannedFiles == 0
                ? NoSearchableFiles(scope)
                : NoMatchingText(result, query, scope);
        }

        var text = new StringBuilder();
        text.Append($"在 {scope} 中匹配 {result.Occurrences} 处：\n");

        foreach (IGrouping<string, TextSearchMatch> group in result.Matches.GroupBy(match => match.File))
        {
            AppendGroup(text, group);

            if (text.Length > TextLimit.Characters)
            {
                break;
            }
        }

        return TextLimit.WithNotice(text.ToString().TrimEnd('\n'), out _);
    }

    /// <summary>唯一性校验失败的交回文本，列出已定位的匹配行。</summary>
    private string Duplicate(TextSearchResult result, TextQuery query, string scope)
    {
        var text = new StringBuilder();
        text.Append($"在 {scope} 中 {query.Pattern} 至少出现 {result.Occurrences} 处，不是唯一匹配：\n");

        foreach (IGrouping<string, TextSearchMatch> group in result.Matches.GroupBy(match => match.File))
        {
            AppendGroup(text, group);
        }

        return text.ToString().TrimEnd('\n');
    }

    /// <summary>唯一匹配要求出现 1 次但没有出现时的交回文本。</summary>
    private static string NoUniqueMatch(TextQuery query, string scope) =>
        $"在 {scope} 中 {query.Pattern} 出现 0 次，不是唯一匹配。";

    /// <summary>追加一个文件的匹配段：相对路径一行，其后是带行号与展示文本的匹配行。</summary>
    private void AppendGroup(StringBuilder text, IGrouping<string, TextSearchMatch> group)
    {
        text.Append(DisplayPath(group.Key));
        text.Append('\n');
        text.Append(LineView.Format(
            [.. group.Select(match => new NumberedLine(match.Line, DisplayText(match)))]));
        text.Append('\n');
    }

    /// <summary>匹配行的展示文本，被截断时在内容后标注完整字符数与展示区间。</summary>
    private static string DisplayText(TextSearchMatch match) =>
        match.Truncated
            ? $"{match.Text}（本行共 {match.FullLength} 字符，展示第 {match.Offset + 1} 至 {match.Offset + match.Text.Length} 字符）"
            : match.Text;

    /// <summary>范围内没有可搜索文件时的交回文本。</summary>
    private static string NoSearchableFiles(string scope) => $"在 {scope} 中没有可搜索的文件。";

    /// <summary>搜索完成但没有匹配时的交回文本，报告扫描的文件数。</summary>
    private static string NoMatchingText(TextSearchResult result, TextQuery query, string scope) =>
        $"在 {scope} 中搜索 {query.Pattern} 没有匹配，已扫描 {result.ScannedFiles} 个文件。";

    /// <summary>文件的展示路径，相对路径基准目录。</summary>
    private string DisplayPath(string file) => Path.GetRelativePath(_scope.Root, file);
}
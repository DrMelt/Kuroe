using ErrorOr;
using Kuroe.Shared;
using Kuroe.Files.Shared;
using Kuroe.Tool.FileAccess;
using Kuroe.Tool.FileAccess.Output;
using Kuroe.Tool.FileAccess.Shared;
using Kuroe.Tool.File.Output;

namespace Kuroe.Tool.File;

/// <summary>在路径基准目录内按字面文本或正则表达式搜索文件内容的工具。路径解析后必须落回工作区内。</summary>
public sealed class SearchTextTool : ITool
{
    /// <summary>判定不唯一需要观察到的出现次数，第二次出现即停止文件内容扫描。</summary>
    private const int DuplicateThreshold = 2;

    private readonly IFileScope _scope;
    private readonly IFileSearch _search;

    /// <summary>在路径范围内按字面文本或正则表达式搜索文件内容。</summary>
    public static ToolDefinition Definition { get; } = new(
        new ToolPath("file/read/SearchText"),
        "在路径基准目录内按字面文本或正则表达式搜索文件内容，按文件分组返回相对路径与带行号的匹配行。正则逐行匹配，^ 与 $ 对应行首与行尾，模式中的换行不匹配，零宽匹配计入出现次数，不支持环视、反向引用、原子组、条件构造、\\G 与平衡组。递归搜索跳过工作区根下的 .kuroe 与 .git，跳过二进制文件，匹配整行，同一行多处只展示一行，输出单行保留包含命中位置的 1000 字符窗口，被截断时在保留内容后标注包含终止符的完整行长与展示的行内容区间，输出超过 8000 字符时截断。",
        [
            new ToolParameter(new ToolName("pattern"), "要搜索的文本，regex 为假时按字面文本解释，为真时按 .NET 正则解释，空拒绝", Required: true),
            new ToolParameter(new ToolName("paths"), "相对路径基准目录的目录或文件路径列表，省略时搜索整个路径基准目录", List: true, Path: true, PathAccess: FileTools.PathAccess),
            new ToolParameter(new ToolName("extensions"), "限定文件后缀名，例如 cs 或 .cs，省略时不限制", List: true),
            new ToolParameter(new ToolName("regex"), "pattern 是否按正则表达式解释，布尔，缺省为假", Flag: true),
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
        : this(FileTools.Scope(binding, files))
    {
    }

    /// <summary>用已建立的文件访问作用域建立载体，搜索使用该作用域内的文本搜索。</summary>
    private SearchTextTool(FileScope scope)
        : this(scope, new FileSearch(scope, FileTools.PathAccess))
    {
    }

    /// <summary>用已建立的文件访问作用域与文本搜索建立载体。</summary>
    public SearchTextTool(IFileScope scope, IFileSearch search)
    {
        _scope = scope;
        _search = search;
        Functions = [Definition.Bind(Search, scope.Paths)];
    }

    /// <summary>按范围、后缀与文本搜索，错误与拒绝以 ErrorOr 表达。unique 为真时要求恰好出现 1 次。</summary>
    private ErrorOr<string> Search(ToolArguments arguments)
    {
        ErrorOr<TextQuery> query = TextQuery.Create(
            arguments.Text(new ToolName("pattern")),
            arguments.List(new ToolName("extensions")),
            arguments.Flag(new ToolName("regex")) == true);
        if (query.IsError)
        {
            return query.ErrorsOrEmptyList;
        }

        IReadOnlyList<string> paths = arguments.List(new ToolName("paths")) ?? [];
        string scope = Scope(paths);
        bool unique = arguments.Flag(new ToolName("unique")) == true;

        ErrorOr<TextSearchResult> result = unique
            ? _search.Search(paths, query.Value, characterBudget: int.MaxValue, occurrenceLimit: DuplicateThreshold)
            : _search.Search(paths, query.Value, TextOutputLimit.Characters);
        if (result.IsError)
        {
            return result.ErrorsOrEmptyList;
        }

        if (unique)
        {
            if (result.Value.ScannedFiles == 0)
            {
                return ToolErrors.Argument(SearchReportText.NoSearchableFiles(scope));
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

        return new SearchReportText(result.Value, query.Value, scope, _scope.Root).ToString();
    }

    /// <summary>搜索范围的展示文本，省略路径时取路径基准。</summary>
    private string Scope(IReadOnlyList<string> paths) =>
        paths.Count == 0 ? _scope.Display(string.Empty) : string.Join('、', paths);

    /// <summary>唯一性校验失败的交回文本，列出已定位的匹配行。</summary>
    private string Duplicate(TextSearchResult result, TextQuery query, string scope)
    {
        string header = $"在 {scope} 中 {query.Pattern} 至少出现 {result.Occurrences} 处，不是唯一匹配：";
        return new SearchMatchListText(result.Matches, header, _scope.Root).ToString();
    }

    /// <summary>唯一匹配要求出现 1 次但没有出现时的交回文本。</summary>
    private static string NoUniqueMatch(TextQuery query, string scope) =>
        $"在 {scope} 中 {query.Pattern} 出现 0 次，不是唯一匹配。";
}
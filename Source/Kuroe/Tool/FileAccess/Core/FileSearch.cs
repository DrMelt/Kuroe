using System.Globalization;
using System.Text;
using ErrorOr;
using Kuroe.Tool.FileAccess.Shared;

namespace Kuroe.Tool.FileAccess;

/// <summary>根目录内的文本搜索：展开目录或文件范围，递归收集候选文件，按后缀与字面文本筛选，匹配行文本累计到预算或出现次数达到上限后停止收集。
/// 路径解析与边界沿用文件作用域，递归时跳过路径准入规则命中的目录与重解析点目录。匹配覆盖整行，展示保留包含命中位置的窗口。</summary>
/// <param name="scope">搜索的文件访问作用域。</param>
/// <param name="access">路径准入规则，null 表示不限制。</param>
public sealed class FileSearch(FileScope scope, ToolPathAccess? access = null) : IFileSearch
{
    private readonly FileScope _scope = scope;
    private readonly FileCandidates _candidates = new(scope, access);

    /// <summary>按范围、后缀与文本搜索，匹配行文本累计到预算或出现次数达到上限后停止收集。单个文件读取失败跳过。</summary>
    /// <param name="paths">搜索的目录或文件范围，空集合表示路径基准目录。</param>
    /// <param name="query">查找条件。</param>
    /// <param name="characterBudget">匹配行文本的字符预算，是渲染长度的下界，只限制扫描量，不决定输出是否截断。</param>
    /// <param name="occurrenceLimit">出现次数上限，累计的非重叠出现次数达到该值后停止扫描。</param>
    public ErrorOr<TextSearchResult> Search(
        IReadOnlyList<string> paths,
        TextQuery query,
        int characterBudget,
        int occurrenceLimit = int.MaxValue)
    {
        if (characterBudget < 1)
        {
            return ToolErrors.Argument("字符预算必须大于 0。");
        }

        if (occurrenceLimit < 1)
        {
            return ToolErrors.Argument("出现次数上限必须大于 0。");
        }

        ErrorOr<IReadOnlyList<string>> candidates = _candidates.Expand(paths);
        if (candidates.IsError)
        {
            return candidates.ErrorsOrEmptyList;
        }

        List<TextSearchMatch> matches = [];
        int scannedFiles = 0;
        int characters = 0;
        int occurrences = 0;

        foreach (string file in candidates.Value)
        {
            if (!query.MatchesExtension(file))
            {
                continue;
            }

            if (characters >= characterBudget || occurrences >= occurrenceLimit)
            {
                break;
            }

            scannedFiles++;
            SearchFile(file, query, matches, ref characters, characterBudget, ref occurrences, occurrenceLimit);
        }

        return new TextSearchResult(matches, scannedFiles, occurrences);
    }

    /// <summary>读取一个文件中的匹配，匹配行文本累计到预算或出现次数达到上限时停止。文件为二进制或读取失败时不收集。</summary>
    private void SearchFile(
        string file,
        TextQuery query,
        List<TextSearchMatch> matches,
        ref int characters,
        int characterBudget,
        ref int occurrences,
        int occurrenceLimit)
    {
        try
        {
            if (!TextFileProbe.IsText(_scope.Workspace, file))
            {
                return;
            }

            using StreamReader reader = new(_scope.Workspace.OpenRead(file), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            int lineNumber = 0;
            while (reader.ReadLine() is { } line)
            {
                lineNumber++;
                int index = query.IndexOf(line);
                if (index < 0)
                {
                    continue;
                }

                occurrences += query.CountOccurrences(line);
                TextSearchMatch match = TextSearchMatch.Create(file, lineNumber, line, index);
                matches.Add(match);
                characters += match.Text.Length + lineNumber.ToString(CultureInfo.InvariantCulture).Length + 2;
                if (characters >= characterBudget || occurrences >= occurrenceLimit)
                {
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 读取失败的文件跳过
        }
    }
}
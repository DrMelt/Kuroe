using ErrorOr;

namespace Kuroe.Tool.FileAccess.Shared;

/// <summary>根目录内的文本搜索：展开路径范围，按后缀与查找模式筛选，匹配行文本累计到预算或出现次数达到上限后停止收集。</summary>
public interface IFileSearch
{
    /// <summary>按范围、后缀与文本搜索。单个文件读取失败跳过。</summary>
    /// <param name="paths">搜索的目录或文件范围，空集合表示路径基准目录。</param>
    /// <param name="query">查找条件。</param>
    /// <param name="characterBudget">匹配行文本的字符预算。</param>
    /// <param name="occurrenceLimit">出现次数上限，累计的非重叠出现次数达到该值后停止扫描。</param>
    ErrorOr<TextSearchResult> Search(
        IReadOnlyList<string> paths,
        TextQuery query,
        int characterBudget,
        int occurrenceLimit = int.MaxValue);
}
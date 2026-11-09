using System.Text;
using Kuroe.Tool.FileAccess.Output;
using Kuroe.Tool.FileAccess.Shared;

namespace Kuroe.Tool.File.Output;

/// <summary>搜索匹配列表文本：按文件分组，相对路径一行，其后是带行号的匹配行。</summary>
/// <param name="Matches">匹配集合。</param>
/// <param name="Header">列表前的标题。</param>
/// <param name="Root">相对路径的基准目录。</param>
public readonly record struct SearchMatchListText(
    IReadOnlyList<TextSearchMatch> Matches,
    string Header,
    string Root)
{
    /// <summary>按文件分组的匹配行文本，末尾不含换行。</summary>
    public override string ToString()
    {
        var text = new StringBuilder();
        text.Append(Header);
        text.Append('\n');
        foreach (IGrouping<string, TextSearchMatch> group in Matches.GroupBy(match => match.File))
        {
            text.Append(Path.GetRelativePath(Root, group.Key));
            text.Append('\n');
            text.Append(new NumberedLineText(
                [.. group.Select(match => new NumberedLine(match.Line, DisplayText(match)))]).ToString());
            text.Append('\n');
        }

        return text.ToString().TrimEnd('\n');
    }

    /// <summary>匹配行的展示文本，被截断时在内容后标注完整行长与展示的行内容区间。</summary>
    private static string DisplayText(TextSearchMatch match) =>
        match.Truncated
            ? $"{match.Text}（本行共 {match.LineLength} 字符，展示行内容第 {match.Offset + 1} 至 {match.Offset + match.Text.Length} 字符）"
            : match.Text;
}
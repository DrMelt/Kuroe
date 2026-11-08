using System.Text;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Workflows.Tasks;

/// <summary>任务目录名：任务键、流程与标题以连字符连接。非法字符替换为下划线，按字符数截断。</summary>
public static class TaskDirectoryName
{
    /// <summary>流程段的字符上限。</summary>
    private const int FlowLimit = 20;

    /// <summary>标题段的字符上限。</summary>
    private const int TitleLimit = 40;

    /// <summary>文件系统不接受的名字字符。</summary>
    private static readonly char[] InvalidChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    /// <summary>按任务键、流程与标题拼出目录名。</summary>
    public static string Of(TaskId id, FlowName flow, string title) =>
        $"{id.Value}-{FlowSegment(flow.Value)}-{TitleSegment(title)}";

    /// <summary>流程段：连字符是段分隔符，段内出现时替换为下划线。</summary>
    private static string FlowSegment(string flow) => Clean(flow, FlowLimit, keepHyphen: false);

    /// <summary>标题段：位于名字末尾，连字符保留。</summary>
    private static string TitleSegment(string title) => Clean(title, TitleLimit, keepHyphen: true);

    /// <summary>替换非法字符与控制字符，去掉首尾空白与结尾点，按字符数截断，结果为空时用下划线占位。</summary>
    private static string Clean(string text, int limit, bool keepHyphen)
    {
        StringBuilder builder = new(Math.Min(text.Length, limit));
        foreach (Rune rune in text.EnumerateRunes())
        {
            builder.Append(IsInvalid(rune, keepHyphen) ? "_" : rune.ToString());
        }

        string cleaned = Truncate(builder.ToString().Trim(), limit).TrimEnd('.', ' ', '_');

        return cleaned.Length == 0 ? "_" : cleaned;
    }

    /// <summary>按字符数截断，不切断代理对。</summary>
    private static string Truncate(string text, int limit)
    {
        StringBuilder builder = new(limit);
        int count = 0;
        foreach (Rune rune in text.EnumerateRunes())
        {
            if (count == limit)
            {
                break;
            }

            builder.Append(rune.ToString());
            count++;
        }

        return builder.ToString();
    }

    /// <summary>字符是否要替换成下划线：控制字符、文件系统非法字符，以及流程段里的连字符。</summary>
    private static bool IsInvalid(Rune rune, bool keepHyphen) =>
        Rune.IsControl(rune)
        || (!keepHyphen && rune.Value == '-')
        || (rune.IsAscii && Array.IndexOf(InvalidChars, (char)rune.Value) >= 0);
}
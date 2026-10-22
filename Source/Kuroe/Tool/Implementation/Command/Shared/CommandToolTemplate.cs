using System.Text.RegularExpressions;

namespace Kuroe.Tool.Command.Shared;

/// <summary>命令模板的文本规则与执行参数缺省。占位符写成 {参数名}，内层不含花括号。</summary>
public static partial class CommandToolTemplate
{
    /// <summary>命令超时的缺省秒数。</summary>
    public const int DefaultTimeoutSeconds = 120;

    /// <summary>命令超时的最大秒数，毫秒换算后不溢出等待参数。</summary>
    public const int MaxTimeoutSeconds = int.MaxValue / 1000;

    /// <summary>交回文本的缺省上限。</summary>
    public const int DefaultOutputLimit = 8000;

    [GeneratedRegex(@"\{([^{}]+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();

    /// <summary>模板里出现的全部占位符名，按出现顺序并去重。</summary>
    public static IReadOnlyList<string> PlaceholdersOf(IEnumerable<CommandToolTemplateItem> template) =>
        [.. template.SelectMany(PlaceholdersOf).Distinct()];

    /// <summary>一个参数项里出现的全部占位符名，按出现顺序。</summary>
    public static IReadOnlyList<string> PlaceholdersOf(CommandToolTemplateItem item) =>
        [.. PlaceholdersOf(item.Text)];

    /// <summary>一段文字里出现的全部占位符名，按出现顺序。</summary>
    internal static IReadOnlyList<string> PlaceholdersOf(string text) =>
        [.. Matches(text).Select(match => match.Groups[1].Value)];

    /// <summary>一段文字里的占位符匹配，供展开按匹配位置替换。</summary>
    internal static MatchCollection Matches(string text) => Placeholder().Matches(text);
}
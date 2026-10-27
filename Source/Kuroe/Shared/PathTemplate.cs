using System.Text.RegularExpressions;

namespace Kuroe.Shared;

/// <summary>路径文本里的变量占位符：写成 {变量名}，内层不含花括号。</summary>
public static partial class PathTemplate
{
    [GeneratedRegex(@"\{([^{}]+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();

    /// <summary>一段文字里出现的全部占位符名，按出现顺序并去重。</summary>
    public static IReadOnlyList<string> Placeholders(string text) =>
        [.. Matches(text).Select(match => match.Groups[1].Value).Distinct()];

    /// <summary>一段文字里的占位符匹配，供按位置替换。</summary>
    public static MatchCollection Matches(string text) => Placeholder().Matches(text);

    /// <summary>按变量替换占位符，未给出的变量保留原样。</summary>
    public static string Expand(string text, IReadOnlyDictionary<string, string> variables) =>
        Placeholder().Replace(text, match =>
            variables.TryGetValue(match.Groups[1].Value, out string? value) ? value : match.Value);

    /// <summary>变量名的非法原因：非空、不含空白与花括号。合法时为空。</summary>
    public static string? InvalidNameReason(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "路径变量名不能为空。";
        }

        return name.Any(char.IsWhiteSpace) || name.Contains('{') || name.Contains('}')
            ? $"路径变量名 {name} 不能含空白与花括号。"
            : null;
    }
}
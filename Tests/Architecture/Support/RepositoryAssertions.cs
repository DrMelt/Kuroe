using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests.Support;

/// <summary>目录源码禁词扫描与工程引用边界的通用断言。</summary>
internal static class RepositoryAssertions
{
    internal static void SourcesExclude(string directory, string category, params string[] forbidden)
    {
        IReadOnlyList<string> files = [.. SourceFiles(directory)];

        Assert.NotEmpty(files);
        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            foreach (string word in forbidden)
            {
                Assert.False(
                    Mentions(text, word),
                    $"{Path.GetFileName(file)} 出现{category} {word}。");
            }
        }
    }

    internal static void ReferencesStayUnder(
        string project,
        string subject,
        string boundary,
        params string[] allowed)
    {
        Assert.All(
            ProjectReferences(project),
            reference => Assert.True(
                allowed.Any(directory => IsUnder(reference, directory)),
                $"{subject} 引用了{boundary}之外的 {reference}。"));
    }

    internal static void ReferencesExclude(
        string project,
        string subject,
        string boundary,
        params string[] excluded)
    {
        Assert.All(
            ProjectReferences(project),
            reference => Assert.False(
                excluded.Any(directory => IsUnder(reference, directory)),
                $"{subject} 引用了{boundary} {reference}。"));
    }

    /// <summary>文本是否出现禁词：禁词末字符属于标识符时要求后继不是标识符字符，命名空间前缀按段匹配。</summary>
    internal static bool Mentions(string text, string word)
    {
        bool bounded = IsIdentifierPart(word[^1]);
        int index = 0;
        while (index < text.Length)
        {
            int found = text.IndexOf(word, index, StringComparison.Ordinal);
            if (found < 0)
            {
                return false;
            }

            int end = found + word.Length;
            if (!bounded || end == text.Length || !IsIdentifierPart(text[end]))
            {
                return true;
            }

            index = end;
        }

        return false;
    }

    private static bool IsIdentifierPart(char value) => char.IsLetterOrDigit(value) || value == '_';
}

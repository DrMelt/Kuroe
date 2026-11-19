using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests.Support;

/// <summary>工程引用边界的通用断言。</summary>
internal static class RepositoryAssertions
{
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
}

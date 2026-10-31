using System.Xml.Linq;
using Xunit;

namespace Kuroe.Architecture.Tests;

/// <summary>仓库结构的约束：IVT 只指向对应测试程序集，Shared 测试只引用同层源码。</summary>
public sealed class RepositoryLayoutTests
{
    private static readonly string Root = FindRoot();
    private static readonly string SourceRoot = Path.Combine(Root, "Source");
    private static readonly string TestsRoot = Path.Combine(Root, "Tests");

    [Fact]
    public void InternalsVisibleTo_only_targets_the_matching_test_assembly()
    {
        HashSet<string> testAssemblies = [.. ProjectFiles(TestsRoot).Select(Path.GetFileNameWithoutExtension).OfType<string>()];

        foreach (string project in ProjectFiles(SourceRoot))
        {
            string expected = $"{Path.GetFileNameWithoutExtension(project)}.Tests";
            foreach (string target in InternalsVisibleTo(project))
            {
                Assert.Equal(expected, target);
                Assert.Contains(target, testAssemblies);
            }
        }
    }

    [Fact]
    public void Shared_test_projects_reference_only_their_layer()
    {
        IReadOnlyList<string> projects = [.. ProjectFiles(TestsRoot).Where(IsSharedTestProject)];

        Assert.NotEmpty(projects);
        foreach (string project in projects)
        {
            IReadOnlyList<string> references = [.. ProjectReferences(project)
                .Where(reference => IsUnder(reference, SourceRoot))];
            IReadOnlyList<string> allowed = SharedDirectories(project);

            Assert.NotEmpty(references);
            Assert.All(references, reference =>
                Assert.True(
                    allowed.Any(directory => IsUnder(reference, directory)),
                    $"{Path.GetFileName(project)} 引用了同层之外的 {reference}。"));
        }
    }

    [Fact]
    public void Compilation_integration_tests_stay_out_of_the_host()
    {
        IReadOnlyList<string> projects = [.. ProjectFiles(TestsRoot).Where(IsCompilationIntegrationTestProject)];

        Assert.NotEmpty(projects);
        foreach (string project in projects)
        {
            IReadOnlyList<string> references = ProjectReferences(project);

            Assert.NotEmpty(references);
            Assert.DoesNotContain(references, reference =>
                IsUnder(reference, Path.Combine(SourceRoot, "Kuroe", "Core")));
            Assert.DoesNotContain(references, reference =>
                IsUnder(reference, Path.Combine(SourceRoot, "Kuroe", "Assistant")));
        }
    }

    private static bool IsSharedTestProject(string project)
    {
        string[] segments = Path.GetRelativePath(TestsRoot, Path.GetDirectoryName(project)!)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        bool flowShared = segments.Length >= 2
            && segments[0] == "Kuroe.Flow"
            && segments.Contains("Shared", StringComparer.Ordinal);
        bool kuroeShared = segments.Length == 2
            && segments[0] == "Kuroe"
            && segments[1] == "Shared";

        return flowShared || kuroeShared;
    }

    private static List<string> SharedDirectories(string project)
    {
        string[] segments = Path.GetRelativePath(TestsRoot, Path.GetDirectoryName(project)!)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        List<string> directories = [];
        for (int length = 1; length <= segments.Length; length++)
        {
            string candidate = Path.Combine([SourceRoot, .. segments[..length], "Shared"]);
            if (Directory.Exists(candidate))
            {
                directories.Add(candidate);
            }
        }

        string sourceDirectory = Path.Combine([SourceRoot, .. segments]);
        if (!directories.Contains(sourceDirectory, StringComparer.Ordinal))
        {
            directories.Add(sourceDirectory);
        }

        return directories;
    }

    private static bool IsCompilationIntegrationTestProject(string project)
    {
        string[] segments = Path.GetRelativePath(TestsRoot, project)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        return segments.Contains("Compilation", StringComparer.Ordinal)
            && segments.Contains("Integration", StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> InternalsVisibleTo(string project) =>
        [.. XDocument.Load(project)
            .Descendants("InternalsVisibleTo")
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>()];

    private static IReadOnlyList<string> ProjectReferences(string project) =>
        [.. XDocument.Load(project)
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>()
            .Select(include => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, include)))];

    private static IEnumerable<string> ProjectFiles(string root) =>
        Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

    private static bool IsUnder(string path, string directory)
    {
        string relative = Path.GetRelativePath(directory, path);

        return relative != ".."
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Kuroe.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("找不到仓库根 Kuroe.slnx。");
    }
}

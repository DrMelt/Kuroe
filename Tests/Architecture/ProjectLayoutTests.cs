using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>项目布局：源项目名、测试项目目录与命名、解决方案清单、命名空间根。</summary>
public sealed partial class ProjectLayoutTests
{
    private static readonly string UnitRoot = Path.Combine(TestsRoot, "Unit");
    private static readonly string IntegrationRoot = Path.Combine(TestsRoot, "Integration");
    private static readonly string ArchitectureRoot = Path.Combine(TestsRoot, "Architecture");
    private static readonly string SupportRoot = Path.Combine(TestsRoot, "Support");

    [GeneratedRegex(@"^\s*namespace\s+([^;{\s]+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex NamespaceDeclaration();

    [Fact(DisplayName = "源项目名与目录一致")]
    public void Source_project_names_match_directories()
    {
        foreach (string project in ProjectFiles(SourceRoot))
        {
            Assert.Equal(ExpectedSourceProjectName(project), Path.GetFileNameWithoutExtension(project));
        }
    }

    [Fact(DisplayName = "测试项目目录与命名镜像源项目")]
    public void Test_projects_mirror_source_projects()
    {
        Dictionary<string, string> sources = ProjectFiles(SourceRoot)
            .ToDictionary(project => Path.GetFileNameWithoutExtension(project), project => project);

        foreach (string project in ProjectFiles(TestsRoot))
        {
            string name = Path.GetFileNameWithoutExtension(project);
            if (name.EndsWith(".TestSupport", StringComparison.Ordinal))
            {
                Assert.True(IsUnder(project, SupportRoot), $"{name} 不在 Tests/Support 下。");
                Assert.Equal(name, Path.GetFileName(DirectoryOf(project)));
                continue;
            }

            if (name == "Kuroe.Architecture.Tests")
            {
                Assert.True(IsUnder(project, ArchitectureRoot), $"{name} 不在 Tests/Architecture 下。");
                continue;
            }

            if (name is "Kuroe.Integration.Tests" or "Kuroe.FlowDescription.Compilation.Integration.Tests")
            {
                Assert.True(IsUnder(project, IntegrationRoot), $"{name} 不在 Tests/Integration 下。");
                continue;
            }

            Assert.True(
                name.EndsWith(".Tests", StringComparison.Ordinal),
                $"{name} 不以 .Tests 结尾。");
            string sourceName = name[..^".Tests".Length];
            Assert.True(sources.ContainsKey(sourceName), $"{name} 没有对应的源项目。");
            string source = sources[sourceName];
            string expectedDirectory = Path.Combine(
                UnitRoot,
                Path.GetRelativePath(SourceRoot, DirectoryOf(source)));

            Assert.Equal(expectedDirectory, DirectoryOf(project));
        }
    }

    [Fact(DisplayName = "解决方案包含全部项目且不重复")]
    public void Solution_lists_every_project_once()
    {
        string[] listed = [.. XDocument.Load(Path.Combine(Root, "Kuroe.slnx"))
            .Descendants("Project")
            .Select(element => element.Attribute("Path")?.Value)
            .OfType<string>()
            .Select(path => Path.GetFullPath(Path.Combine(
                Root,
                path.Replace('/', Path.DirectorySeparatorChar))))];
        string[] projects = [.. ProjectFiles(SourceRoot), .. ProjectFiles(TestsRoot)];

        Assert.Equal(
            projects.Order(StringComparer.OrdinalIgnoreCase),
            listed.Order(StringComparer.OrdinalIgnoreCase));
        Assert.Equal(listed.Length, listed.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact(DisplayName = "命名空间根与项目名一致")]
    public void Namespace_roots_match_project_names()
    {
        foreach (string project in ProjectFiles(SourceRoot).Concat(ProjectFiles(TestsRoot)))
        {
            string name = Path.GetFileNameWithoutExtension(project);
            string expected = name == "Kuroe.Runner.Shared" ? "Kuroe.Runner" : name;

            foreach (string file in Directory
                .EnumerateFiles(DirectoryOf(project), "*.cs", SearchOption.AllDirectories)
                .Where(file => !IsBuildOutput(file)))
            {
                string text = File.ReadAllText(file);
                foreach (Match match in NamespaceDeclaration().Matches(text))
                {
                    string value = match.Groups[1].Value;

                    Assert.True(
                        value == expected || value.StartsWith($"{expected}.", StringComparison.Ordinal),
                        $"{file} 的命名空间 {value} 不以 {expected} 为根。");
                }
            }
        }
    }

    private static string ExpectedSourceProjectName(string project)
    {
        string relative = Path.GetRelativePath(SourceRoot, DirectoryOf(project));
        string[] segments = relative.Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries);
        if (segments[^1] == "Core")
        {
            segments = segments[..^1];
        }

        return string.Join('.', segments);
    }
}
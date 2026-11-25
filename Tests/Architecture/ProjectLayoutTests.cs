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

            foreach (string file in Directory
                .EnumerateFiles(DirectoryOf(project), "*.cs", SearchOption.AllDirectories)
                .Where(file => !IsBuildOutput(file)))
            {
                string text = File.ReadAllText(file);
                foreach (Match match in NamespaceDeclaration().Matches(text))
                {
                    string value = match.Groups[1].Value;

                    Assert.True(
                        value == name || value.StartsWith($"{name}.", StringComparison.Ordinal),
                        $"{file} 的命名空间 {value} 不以 {name} 为根。");
                }
            }
        }
    }

    [Fact(DisplayName = "源项目按产品与通用分组")]
    public void Source_projects_are_grouped_into_product_and_generic_directories()
    {
        string[] generic = ["Files", "Process"];

        foreach (string project in ProjectFiles(SourceRoot))
        {
            string[] segments = Segments(project);
            bool product = segments[0] == "Kuroe";
            bool genericRoot = segments.Length > 1 && generic.Contains(segments[0], StringComparer.Ordinal);

            Assert.True(
                product || genericRoot,
                $"{project} 不在产品目录 Kuroe 或通用目录 Files、Process 下。");
        }
    }

    [Fact(DisplayName = "域目录与结构文档一致")]
    public void Domain_subdirectories_match_the_structural_map()
    {
        AssertDomain("Kuroe/Flow", "Catalog", "Compilation", "Control", "Graph", "Library", "Persistence", "Records");
        AssertDomain("Kuroe/Host", "Ability", "Assistant", "Composition", "FlowIntegration", "Platform");
        AssertDomain("Kuroe/Host/Ability", "Node", "Runner", "Tool");
    }

    [Fact(DisplayName = "通用项目不引用产品项目")]
    public void Generic_projects_do_not_reference_product_projects()
    {
        foreach (string project in ProjectFiles(SourceRoot).Where(project => Segments(project)[0] != "Kuroe"))
        {
            Assert.All(
                ProjectReferences(project),
                reference => Assert.False(
                    IsUnder(reference, KuroeRoot),
                    $"{project} 引用了产品项目 {reference}。"));
        }
    }

    private static void AssertDomain(string relative, params string[] expected)
    {
        string directory = Path.Combine(SourceRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        string[] actual = [.. Directory.EnumerateDirectories(directory)
            .Where(path => !IsBuildOutput(path))
            .Select(path => new DirectoryInfo(path).Name)
            .Order(StringComparer.Ordinal)];
        string[] want = [.. expected.Order(StringComparer.Ordinal)];

        Assert.True(
            want.SequenceEqual(actual, StringComparer.Ordinal),
            $"{directory} 的子目录与结构文档不一致，见 docs/dev/architecture/functional-boundary/index.md。实际：{string.Join('、', actual)}");
    }

    private static string[] Segments(string project) =>
        Path.GetRelativePath(SourceRoot, DirectoryOf(project))
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

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
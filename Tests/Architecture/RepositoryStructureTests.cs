using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>仓库与测试工程结构：InternalsVisibleTo 指向、共享测试工程引用、编译集成测试归属。</summary>
public sealed class RepositoryStructureTests
{
    [Fact(DisplayName = "InternalsVisibleTo 只指向对应测试程序集")]
    public void InternalsVisibleTo_only_targets_the_matching_test_assembly()
    {
        foreach (string project in ProjectFiles(SourceRoot))
        {
            string expected = $"{Path.GetFileNameWithoutExtension(project)}.Tests";

            foreach (string target in InternalsVisibleTo(project))
            {
                Assert.Equal(expected, target);
            }
        }
    }

    [Fact(DisplayName = "共享测试项目只引用共享源码与工具内核")]
    public void Shared_test_projects_reference_only_shared_source_and_tool_kernel()
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
                    $"{Path.GetFileName(project)} 引用了共享源码之外的 {reference}。"));
        }
    }

    [Fact(DisplayName = "编译集成测试不引用宿主")]
    public void Compilation_integration_tests_stay_out_of_the_host()
    {
        IReadOnlyList<string> projects = [.. ProjectFiles(TestsRoot).Where(IsCompilationIntegrationTestProject)];

        Assert.NotEmpty(projects);
        foreach (string project in projects)
        {
            IReadOnlyList<string> references = ProjectReferences(project);

            Assert.NotEmpty(references);
            Assert.DoesNotContain(references, reference =>
                IsUnder(reference, Path.Combine(SourceRoot, "Kuroe", "Composition", "Core")));
            Assert.DoesNotContain(references, reference =>
                IsUnder(reference, Path.Combine(SourceRoot, "Kuroe", "Assistant")));
        }
    }

    private static bool IsSharedTestProject(string project) =>
        Path.GetRelativePath(TestsRoot, DirectoryOf(project))
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
            .Contains("Shared", StringComparer.Ordinal);

    private static List<string> SharedDirectories(string project)
    {
        string[] segments = Path.GetRelativePath(TestsRoot, DirectoryOf(project))
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (segments[0] == "Unit")
        {
            segments = segments[1..];
        }

        List<string> directories = [];
        string[] area = segments[0] == "Kuroe.Kernel" ? segments[..2] : segments[..1];
        string component = Path.Combine([SourceRoot, .. area]);
        foreach (string directory in Directory.EnumerateDirectories(component, "Shared", SearchOption.AllDirectories))
        {
            if (!IsBuildOutput(directory))
            {
                directories.Add(directory);
            }
        }

        // 执行、任务与表现的共享契约是中性基础层，任何共享层都可引用
        // 流程描述与图模型的共享层是跨流程的中性契约，共享测试可直接使用
        foreach (string neutral in new[] { "Execution", "Tasks", "Presentation" })
        {
            string contracts = Path.Combine(KernelRoot, neutral);
            if (Directory.Exists(contracts))
            {
                directories.AddRange(Directory
                    .EnumerateDirectories(contracts, "Shared", SearchOption.AllDirectories)
                    .Where(directory => !IsBuildOutput(directory)));
            }
        }

        string description = Path.Combine(SourceRoot, "Kuroe.FlowDescription");
        if (Directory.Exists(description))
        {
            directories.AddRange(Directory
                .EnumerateDirectories(description, "Shared", SearchOption.AllDirectories)
                .Where(directory => !IsBuildOutput(directory)));
        }

        foreach (string relative in new[]
        {
            Path.Combine("Graph", "Shared"),
            Path.Combine("Records", "Shared"),
        })
        {
            string directory = Path.Combine(SourceRoot, "Kuroe.Flow", relative);
            if (Directory.Exists(directory))
            {
                directories.Add(directory);
            }
        }

        string sourceDirectory = Path.Combine([SourceRoot, .. segments]);
        if (!directories.Contains(sourceDirectory, StringComparer.Ordinal))
        {
            directories.Add(sourceDirectory);
        }

        if (segments.Length >= 2 && segments[0] == "Kuroe" && segments[1] == "Tool")
        {
            directories.Add(Path.Combine(SourceRoot, "Kuroe", "Tool", "Core"));
        }

        return directories;
    }

    private static bool IsCompilationIntegrationTestProject(string project)
    {
        string[] segments = Path.GetRelativePath(TestsRoot, project)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        string name = Path.GetFileNameWithoutExtension(project);

        return name.EndsWith(".Compilation.Integration.Tests", StringComparison.Ordinal)
            || segments.Contains("Compilation", StringComparer.Ordinal)
                && segments.Contains("Integration", StringComparer.Ordinal);
    }
}

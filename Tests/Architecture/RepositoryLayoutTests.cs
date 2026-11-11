using System.Xml.Linq;
using Xunit;

namespace Kuroe.Architecture.Tests;

/// <summary>仓库结构的约束：IVT 只指向对应测试程序集，Shared 测试只引用共享源码与工具内核，文件访问 Shared 不引用同模块 Core 与 Kuroe.Files。</summary>
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

    [Fact]
    public void FileAccess_shared_project_stays_free_of_core_and_file_io()
    {
        string fileAccess = Path.Combine(SourceRoot, "Kuroe", "Tool", "FileAccess");
        string shared = Path.Combine(fileAccess, "Shared", "Kuroe.Tool.FileAccess.Shared.csproj");
        string core = Path.Combine(fileAccess, "Core", "Kuroe.Tool.FileAccess.csproj");
        IReadOnlyList<string> sharedReferences = ProjectReferences(shared);

        Assert.DoesNotContain(sharedReferences, reference => IsUnder(reference, Path.Combine(fileAccess, "Core")));
        Assert.DoesNotContain(sharedReferences, reference =>
            IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Files")));
        Assert.Contains(shared, ProjectReferences(core));
    }

    [Fact]
    public void Execution_shared_project_stays_free_of_graph_and_control_references()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Execution",
            "Shared",
            "Kuroe.Flow.Execution.Shared.csproj");

        Assert.Empty(ProjectReferences(project));
    }

    [Fact]
    public void Presentation_shared_project_stays_free_of_graph_and_control_references()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Presentation",
            "Shared",
            "Kuroe.Flow.Presentation.Shared.csproj");

        Assert.Empty(ProjectReferences(project));
    }

    [Fact]
    public void Execution_core_references_only_execution_shared()
    {
        string shared = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Execution",
            "Shared",
            "Kuroe.Flow.Execution.Shared.csproj");
        string core = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Execution",
            "Core",
            "Kuroe.Flow.Execution.csproj");
        IReadOnlyList<string> references = ProjectReferences(core);

        Assert.Single(references);
        Assert.Contains(shared, references);
    }

    [Fact]
    public void Tasks_shared_project_stays_free_of_graph_references()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Tasks",
            "Shared",
            "Kuroe.Flow.Tasks.Shared.csproj");
        IReadOnlyList<string> references = ProjectReferences(project);

        Assert.DoesNotContain(references, reference =>
            IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow", "Shared")));
        Assert.DoesNotContain(references, reference =>
            IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow", "Core")));
        Assert.DoesNotContain(references, reference =>
            IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow", "Execution")));
    }

    [Fact]
    public void Execution_records_project_references_only_execution_shared()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Execution",
            "Records",
            "Shared",
            "Kuroe.Flow.Execution.Records.Shared.csproj");

        IReadOnlyList<string> references = ProjectReferences(project);

        Assert.Single(references);
        Assert.Contains(
            Path.Combine(SourceRoot, "Kuroe.Flow", "Execution", "Shared", "Kuroe.Flow.Execution.Shared.csproj"),
            references);
    }

    [Fact]
    public void Tasks_records_project_references_only_task_and_execution_records()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Tasks",
            "Records",
            "Shared",
            "Kuroe.Flow.Tasks.Records.Shared.csproj");
        IReadOnlyList<string> references = ProjectReferences(project);
        string tasks = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Shared");
        string executionRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Execution", "Records", "Shared");

        Assert.All(references, reference =>
            Assert.True(
                IsUnder(reference, tasks) || IsUnder(reference, executionRecords),
                $"{Path.GetFileName(project)} 引用了任务记录边界之外的 {reference}。"));
    }

    [Fact]
    public void Graph_records_project_stays_free_of_runtime_and_host()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Graph",
            "Records",
            "Shared",
            "Kuroe.Flow.Graph.Records.Shared.csproj");
        IReadOnlyList<string> references = ProjectReferences(project);
        string graph = Path.Combine(SourceRoot, "Kuroe.Flow", "Shared");
        string tasksRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Records", "Shared");
        string execution = Path.Combine(SourceRoot, "Kuroe.Flow", "Execution", "Shared");

        Assert.All(references, reference =>
            Assert.True(
                IsUnder(reference, graph) || IsUnder(reference, tasksRecords) || IsUnder(reference, execution),
                $"{Path.GetFileName(project)} 引用了图记录边界之外的 {reference}。"));
    }

    [Fact]
    public void Persistence_projects_reference_only_shared_boundaries()
    {
        string persistence = Path.Combine(SourceRoot, "Kuroe.Flow", "Persistence");
        string flowContracts = Path.Combine(SourceRoot, "Kuroe.Flow", "Shared");
        string taskContracts = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Shared");
        string executionContracts = Path.Combine(SourceRoot, "Kuroe.Flow", "Execution", "Shared");
        string executionRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Execution", "Records", "Shared");
        string taskRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Records", "Shared");
        string graphRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Graph", "Records", "Shared");

        foreach (string project in ProjectFiles(persistence))
        {
            IReadOnlyList<string> references = [.. ProjectReferences(project)
                .Where(reference => IsUnder(reference, SourceRoot))];

            Assert.All(references, reference =>
                Assert.True(
                    IsPersistenceBoundary(reference, persistence)
                        || IsUnder(reference, flowContracts)
                        || IsUnder(reference, taskContracts)
                        || IsUnder(reference, executionContracts)
                        || IsUnder(reference, executionRecords)
                        || IsUnder(reference, taskRecords)
                        || IsUnder(reference, graphRecords),
                    $"{Path.GetFileName(project)} 引用了持久化边界之外的 {reference}。"));
        }
    }

    private static bool IsPersistenceBoundary(string project, string persistence)
    {
        string directory = DirectoryOf(project);

        return IsUnder(directory, persistence)
            && string.Equals(Path.GetFileName(directory), "Shared", StringComparison.Ordinal);
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

    private static bool IsSharedTestProject(string project) =>
        Path.GetRelativePath(TestsRoot, DirectoryOf(project))
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
            .Contains("Shared", StringComparer.Ordinal);

    private static List<string> SharedDirectories(string project)
    {
        string[] segments = Path.GetRelativePath(TestsRoot, DirectoryOf(project))
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        List<string> directories = [];
        string component = Path.Combine(SourceRoot, segments[0]);
        foreach (string directory in Directory.EnumerateDirectories(component, "Shared", SearchOption.AllDirectories))
        {
            if (!IsBuildOutput(directory))
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

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

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
            // csproj 中的 ProjectReference 固定用反斜杠
            .Select(include => Path.GetFullPath(Path.Combine(
                DirectoryOf(project),
                include.Replace('\\', Path.DirectorySeparatorChar))))];

    /// <summary>项目文件的所在目录，项目路径总是带目录。</summary>
    private static string DirectoryOf(string project) =>
        Path.GetDirectoryName(project) ?? throw new InvalidOperationException($"{project} 没有所在目录。");

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

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
        foreach (string project in ProjectFiles(SourceRoot))
        {
            string expected = $"{Path.GetFileNameWithoutExtension(project)}.Tests";

            foreach (string target in InternalsVisibleTo(project))
            {
                Assert.Equal(expected, target);
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
    public void Execution_shared_project_references_only_tasks_shared()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Execution",
            "Shared",
            "Kuroe.Flow.Execution.Shared.csproj");
        string tasks = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Shared", "Kuroe.Flow.Tasks.Shared.csproj");

        Assert.Equal([tasks], ProjectReferences(project));
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

        string execution = Path.Combine(SourceRoot, "Kuroe.Flow", "Execution", "Shared");
        string tasks = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Shared");

        Assert.All(
            ProjectReferences(project),
            reference => Assert.True(
                IsUnder(reference, execution) || IsUnder(reference, tasks),
                $"Kuroe.Flow.Presentation.Shared 引用了中性契约之外的 {reference}。"));
    }

    [Fact]
    public void Execution_core_references_only_execution_shared_and_tasks_shared()
    {
        string shared = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Execution",
            "Shared",
            "Kuroe.Flow.Execution.Shared.csproj");
        string tasks = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Tasks",
            "Shared",
            "Kuroe.Flow.Tasks.Shared.csproj");
        string core = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Execution",
            "Core",
            "Kuroe.Flow.Execution.csproj");
        IReadOnlyList<string> references = ProjectReferences(core);

        Assert.Equal(2, references.Count);
        Assert.Contains(shared, references);
        Assert.Contains(tasks, references);
    }

    [Fact]
    public void Execution_projects_stay_free_of_graph_and_node_semantics()
    {
        string execution = Path.Combine(SourceRoot, "Kuroe.Flow", "Execution");
        string[] forbidden =
        [
            "NodePath",
            "NodeGraph",
            "ExecutableNode",
            "ModelPort",
            "GraphExecution",
            "IExecutionTargetResolver",
            "RuntimeGraph.Execution",
            "IsConstant",
            "RequiresSubmission",
            "PortMode",
            "NodeEmission",
        ];

        Assert.NotEmpty(SourceFiles(execution));
        foreach (string file in SourceFiles(execution))
        {
            string text = File.ReadAllText(file);
            foreach (string word in forbidden)
            {
                Assert.False(
                    text.Contains(word, StringComparison.Ordinal),
                    $"{Path.GetFileName(file)} 出现图或节点语义 {word}。");
            }
        }
    }

    [Fact]
    public void Flow_runtime_stays_free_of_graph_control()
    {
        string file = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Runtime",
            "Core",
            "FlowRuntime.cs");
        string text = File.ReadAllText(file);

        foreach (string word in (string[])["TaskDriver", "TaskRuntime", "GraphExecution", "NodeGraph"])
        {
            Assert.DoesNotContain(word, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Flow_runtime_lives_in_its_own_project()
    {
        string runtime = Path.Combine(SourceRoot, "Kuroe.Flow", "Runtime", "Core", "FlowRuntime.cs");
        string project = Path.Combine(SourceRoot, "Kuroe.Flow", "Runtime", "Core", "Kuroe.Flow.Runtime.csproj");
        string old = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Control",
            "Graph",
            "Runtime",
            "Engine",
            "FlowRuntime.cs");

        Assert.True(File.Exists(runtime));
        Assert.True(File.Exists(project));
        Assert.False(File.Exists(old));

        IReadOnlyList<string> references = ProjectReferences(project);

        Assert.DoesNotContain(references, reference =>
            IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Graph")));
        Assert.DoesNotContain(references, reference =>
            IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow", "Node")));
    }

    [Fact]
    public void Core_project_does_not_reference_execution_core()
    {
        string project = Path.Combine(SourceRoot, "Kuroe.Flow", "Core", "Kuroe.Flow.csproj");

        Assert.DoesNotContain(
            ProjectReferences(project),
            reference => IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow", "Execution", "Core")));
    }

    [Fact]
    public void Control_shared_stays_free_of_graph_runtime()
    {
        string control = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Shared");
        string[] forbidden =
        [
            "TaskRuntime",
            "GraphExecution",
            "NodeGraph",
            "ExecutableNode",
            "GraphFlow",
            "ModelPort",
        ];

        Assert.NotEmpty(SourceFiles(control));
        foreach (string file in SourceFiles(control))
        {
            string text = File.ReadAllText(file);
            foreach (string word in forbidden)
            {
                Assert.False(
                    text.Contains(word, StringComparison.Ordinal),
                    $"{Path.GetFileName(file)} 出现图运行期语义 {word}。");
            }
        }
    }

    [Fact]
    public void Core_project_does_not_reference_graph_control()
    {
        string project = Path.Combine(SourceRoot, "Kuroe.Flow", "Core", "Kuroe.Flow.csproj");

        Assert.DoesNotContain(
            ProjectReferences(project),
            reference => reference.Contains("Control\\Graph", StringComparison.OrdinalIgnoreCase)
                || reference.Contains("Control/Graph", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Core_source_stays_free_of_graph_runtime()
    {
        string core = Path.Combine(SourceRoot, "Kuroe.Flow", "Core");
        string[] forbidden =
        [
            "TaskDriver",
            "TaskRuntime",
            "GraphExecution",
            "NodeGraph",
            "ExecutableNode",
            "GraphFlow",
        ];

        Assert.NotEmpty(SourceFiles(core));
        foreach (string file in SourceFiles(core))
        {
            string text = File.ReadAllText(file);
            foreach (string word in forbidden)
            {
                Assert.False(
                    text.Contains(word, StringComparison.Ordinal),
                    $"{Path.GetFileName(file)} 出现图运行期语义 {word}。");
            }
        }
    }

    [Fact]
    public void Graph_runtime_lives_in_control_graph()
    {
        string core = Path.Combine(SourceRoot, "Kuroe.Flow", "Core");
        string graph = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Graph");

        Assert.True(File.Exists(Path.Combine(core, "Runtime", "Tasks", "WorkTask.cs")));
        Assert.False(File.Exists(Path.Combine(graph, "Runtime", "Tasks", "WorkTask.cs")));
        Assert.True(File.Exists(Path.Combine(graph, "Runtime", "Engine", "TaskDriver.cs")));
        Assert.True(File.Exists(Path.Combine(graph, "Runtime", "Graph", "TaskRuntime.cs")));
    }

    [Fact]
    public void Task_registry_has_no_restorer_surface()
    {
        string file = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Core",
            "Runtime",
            "Tasks",
            "TaskRegistry.cs");

        Assert.DoesNotContain("Restorers", File.ReadAllText(file), StringComparison.Ordinal);
    }

    [Fact]
    public void Node_projects_do_not_reference_flow_implementation()
    {
        string nodes = Path.Combine(SourceRoot, "Kuroe", "Node");
        string execution = Path.Combine(SourceRoot, "Kuroe.Flow", "Execution", "Core");
        string flow = Path.Combine(SourceRoot, "Kuroe.Flow", "Core");
        string graphRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Graph", "Records");

        Assert.NotEmpty(ProjectFiles(nodes));
        foreach (string project in ProjectFiles(nodes))
        {
            IReadOnlyList<string> references = ProjectReferences(project);

            Assert.DoesNotContain(references, reference => IsUnder(reference, execution));
            Assert.DoesNotContain(references, reference => IsUnder(reference, flow));
            Assert.DoesNotContain(references, reference => IsUnder(reference, graphRecords));
        }
    }

    [Fact]
    public void Node_projects_reference_node_shared()
    {
        string nodes = Path.Combine(SourceRoot, "Kuroe", "Node");
        string nodeShared = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Node",
            "Shared",
            "Kuroe.Flow.Node.Shared.csproj");

        Assert.NotEmpty(ProjectFiles(nodes));
        foreach (string project in ProjectFiles(nodes))
        {
            Assert.Contains(nodeShared, ProjectReferences(project));
        }
    }

    [Fact]
    public void Node_shared_project_references_only_contract_layers()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Node",
            "Shared",
            "Kuroe.Flow.Node.Shared.csproj");
        string tasks = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Shared");
        string execution = Path.Combine(SourceRoot, "Kuroe.Flow", "Execution", "Shared");
        string shared = Path.Combine(SourceRoot, "Kuroe.Flow", "Shared");

        Assert.All(
            ProjectReferences(project),
            reference => Assert.True(
                IsUnder(reference, tasks) || IsUnder(reference, execution) || IsUnder(reference, shared),
                $"Kuroe.Flow.Node.Shared 引用了契约层之外的 {reference}。"));
    }

    [Fact]
    public void Flow_shared_project_references_only_neutral_shared()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Shared",
            "Kuroe.Flow.Shared.csproj");
        string tasks = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Shared");
        string execution = Path.Combine(SourceRoot, "Kuroe.Flow", "Execution", "Shared");

        Assert.All(
            ProjectReferences(project),
            reference => Assert.True(
                IsUnder(reference, tasks) || IsUnder(reference, execution),
                $"Kuroe.Flow.Shared 引用了中性共享之外的 {reference}。"));
    }

    [Fact]
    public void Control_shared_project_references_only_contract_layers()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Control",
            "Shared",
            "Kuroe.Flow.Control.Shared.csproj");
        string nodes = Path.Combine(SourceRoot, "Kuroe.Flow", "Node", "Shared");
        string tasks = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Shared");
        string taskPresentation = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Presentation", "Shared");
        string execution = Path.Combine(SourceRoot, "Kuroe.Flow", "Execution", "Shared");
        string executionRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Execution", "Records", "Shared");
        string presentation = Path.Combine(SourceRoot, "Kuroe.Flow", "Presentation", "Shared");
        string shared = Path.Combine(SourceRoot, "Kuroe.Flow", "Shared");
        string taskRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Records", "Shared");

        Assert.All(
            ProjectReferences(project),
            reference => Assert.True(
                IsUnder(reference, nodes)
                    || IsUnder(reference, tasks)
                    || IsUnder(reference, taskPresentation)
                    || IsUnder(reference, execution)
                    || IsUnder(reference, executionRecords)
                    || IsUnder(reference, presentation)
                    || IsUnder(reference, taskRecords)
                    || IsUnder(reference, shared),
                $"Kuroe.Flow.Control.Shared 引用了契约层之外的 {reference}。"));
    }

    [Fact]
    public void Task_presentation_references_only_contract_layers()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Tasks",
            "Presentation",
            "Shared",
            "Kuroe.Flow.Tasks.Presentation.Shared.csproj");
        string tasks = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Shared");
        string execution = Path.Combine(SourceRoot, "Kuroe.Flow", "Execution", "Shared");
        string presentation = Path.Combine(SourceRoot, "Kuroe.Flow", "Presentation", "Shared");
        string shared = Path.Combine(SourceRoot, "Kuroe.Flow", "Shared");

        Assert.All(
            ProjectReferences(project),
            reference => Assert.True(
                IsUnder(reference, tasks)
                    || IsUnder(reference, execution)
                    || IsUnder(reference, presentation)
                    || IsUnder(reference, shared),
                $"Kuroe.Flow.Tasks.Presentation.Shared 引用了契约层之外的 {reference}。"));
    }

    [Fact]
    public void Task_presentation_stays_free_of_graph_control()
    {
        string presentation = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Presentation", "Shared");
        string[] forbidden = ["TaskDriver", "TaskRuntime", "NodeGraph", "Control.Graph"];

        Assert.NotEmpty(SourceFiles(presentation));
        foreach (string file in SourceFiles(presentation))
        {
            string text = File.ReadAllText(file);
            foreach (string word in forbidden)
            {
                Assert.False(
                    text.Contains(word, StringComparison.Ordinal),
                    $"{Path.GetFileName(file)} 出现图控制语义 {word}。");
            }
        }
    }

    [Fact]
    public void Task_presentation_stays_free_of_node_and_graph_types()
    {
        string presentation = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Presentation", "Shared");
        string[] forbidden =
        [
            "NodePath",
            "NodeGraph",
            "ExecutableSnapshot",
            "ExecutableStateSnapshot",
            "ContainerSnapshot",
            "FlowExecutableView",
        ];

        Assert.NotEmpty(SourceFiles(presentation));
        foreach (string file in SourceFiles(presentation))
        {
            string text = File.ReadAllText(file);
            foreach (string word in forbidden)
            {
                Assert.False(
                    text.Contains(word, StringComparison.Ordinal),
                    $"{Path.GetFileName(file)} 出现节点或图类型 {word}。");
            }
        }

        string snapshot = File.ReadAllText(Path.Combine(presentation, "TaskSnapshot.cs"));
        Assert.DoesNotContain("Executables", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutableStates", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("Containers", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Execution_index_stays_free_of_flow_payload()
    {
        string file = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Core",
            "Runtime",
            "ExecutionIndex.cs");
        string text = File.ReadAllText(file);

        Assert.DoesNotContain("Payload", text, StringComparison.Ordinal);
        Assert.DoesNotContain("object?", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Execution_gateway_stays_neutral()
    {
        string file = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Control",
            "Shared",
            "IFlowExecutionGateway.cs");

        Assert.DoesNotContain("Attach", File.ReadAllText(file), StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_restore_resolves_by_record_not_kind()
    {
        string contract = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Control",
            "Shared",
            "IFlowFrameworkSource.cs");
        string source = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Core",
            "Runtime",
            "Tasks",
            "FlowFrameworkSource.cs");
        string restore = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Core",
            "Runtime",
            "Tasks",
            "Persistence",
            "TaskRestoreRuntime.cs");

        Assert.Contains("Find(FlowRecord record)", File.ReadAllText(contract), StringComparison.Ordinal);
        Assert.DoesNotContain("Find(string kind)", File.ReadAllText(contract), StringComparison.Ordinal);
        Assert.DoesNotContain("Find(record.Flow.Kind)", File.ReadAllText(restore), StringComparison.Ordinal);
        Assert.Contains("Handles(record)", File.ReadAllText(source), StringComparison.Ordinal);
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
    public void Dialogue_host_depends_on_code_flow_not_graph_control()
    {
        string dialogue = Path.Combine(SourceRoot, "Kuroe", "Assistant", "Core", "Dialogue");
        IReadOnlyList<string> files = [.. SourceFiles(dialogue)];

        Assert.NotEmpty(files);
        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            Assert.DoesNotContain("GraphFlowFramework", text, StringComparison.Ordinal);
            Assert.DoesNotContain("PrepareCompiled", text, StringComparison.Ordinal);
            Assert.DoesNotContain("IFlowDescriptionCompiler", text, StringComparison.Ordinal);
            Assert.DoesNotContain("FlowDocument", text, StringComparison.Ordinal);
        }

        string project = Path.Combine(SourceRoot, "Kuroe", "Assistant", "Core", "Kuroe.Assistant.csproj");
        IReadOnlyList<string> references = ProjectReferences(project);

        Assert.DoesNotContain(references, reference =>
            IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Graph")));
        Assert.DoesNotContain(references, reference =>
            IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow", "Compilation", "Shared")));
    }

    [Fact]
    public void Tasks_records_project_references_only_task_execution_and_shared()
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
        string shared = Path.Combine(SourceRoot, "Kuroe.Flow", "Shared");

        Assert.All(references, reference =>
            Assert.True(
                IsUnder(reference, tasks) || IsUnder(reference, executionRecords) || IsUnder(reference, shared),
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
        string tasks = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Shared");
        string tasksRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Records", "Shared");
        string execution = Path.Combine(SourceRoot, "Kuroe.Flow", "Execution", "Shared");
        string nodes = Path.Combine(SourceRoot, "Kuroe.Flow", "Node", "Shared");

        Assert.All(references, reference =>
            Assert.True(
                IsUnder(reference, graph)
                    || IsUnder(reference, tasks)
                    || IsUnder(reference, tasksRecords)
                    || IsUnder(reference, execution)
                    || IsUnder(reference, nodes),
                $"{Path.GetFileName(project)} 引用了图记录边界之外的 {reference}。"));
    }

    [Fact]
    public void Persistence_projects_reference_only_shared_boundaries()
    {
        string persistence = Path.Combine(SourceRoot, "Kuroe.Flow", "Persistence");
        string flowContracts = Path.Combine(SourceRoot, "Kuroe.Flow", "Shared");
        string taskContracts = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Shared");
        string taskPresentation = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Presentation", "Shared");
        string executionContracts = Path.Combine(SourceRoot, "Kuroe.Flow", "Execution", "Shared");
        string executionRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Execution", "Records", "Shared");
        string taskRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Records", "Shared");
        string graphRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Graph", "Records", "Shared");
        string nodeContracts = Path.Combine(SourceRoot, "Kuroe.Flow", "Node", "Shared");

        foreach (string project in ProjectFiles(persistence))
        {
            IReadOnlyList<string> references = [.. ProjectReferences(project)
                .Where(reference => IsUnder(reference, SourceRoot))];

            Assert.All(references, reference =>
                Assert.True(
                    IsPersistenceBoundary(reference, persistence)
                        || IsUnder(reference, flowContracts)
                        || IsUnder(reference, taskContracts)
                        || IsUnder(reference, taskPresentation)
                        || IsUnder(reference, executionContracts)
                        || IsUnder(reference, executionRecords)
                        || IsUnder(reference, taskRecords)
                        || IsUnder(reference, graphRecords)
                        || IsUnder(reference, nodeContracts),
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

    [Fact]
    public void Control_shared_project_references_only_neutral_contracts()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Control",
            "Shared",
            "Kuroe.Flow.Control.Shared.csproj");
        string tasks = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Shared");
        string tasksPresentation = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Presentation", "Shared");
        string tasksRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Records", "Shared");
        string execution = Path.Combine(SourceRoot, "Kuroe.Flow", "Execution", "Shared");
        string executionRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Execution", "Records", "Shared");
        string presentation = Path.Combine(SourceRoot, "Kuroe.Flow", "Presentation", "Shared");

        Assert.All(
            ProjectReferences(project),
            reference => Assert.True(
                IsUnder(reference, tasks)
                    || IsUnder(reference, tasksPresentation)
                    || IsUnder(reference, tasksRecords)
                    || IsUnder(reference, execution)
                    || IsUnder(reference, executionRecords)
                    || IsUnder(reference, presentation),
                $"Kuroe.Flow.Control.Shared 引用了中性契约之外的 {reference}。"));
    }

    [Fact]
    public void Control_shared_uses_only_neutral_namespaces()
    {
        string control = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Shared");
        string[] forbidden =
        [
            "Kuroe.Flow.Node.RuntimeGraph",
            "Kuroe.Flow.Node.StaticGraph",
            "Kuroe.Flow.Control.Graph",
            "Kuroe.Flow.Node.Shared",
        ];

        Assert.NotEmpty(SourceFiles(control));
        foreach (string file in SourceFiles(control))
        {
            string text = File.ReadAllText(file);
            foreach (string word in forbidden)
            {
                Assert.False(
                    text.Contains(word, StringComparison.Ordinal),
                    $"{Path.GetFileName(file)} 出现图命名空间 {word}。");
            }
        }
    }

    [Fact]
    public void Tasks_presentation_uses_only_neutral_namespaces()
    {
        string presentation = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Presentation", "Shared");
        string[] forbidden =
        [
            "Kuroe.Flow.Node.RuntimeGraph",
            "Kuroe.Flow.Node.StaticGraph",
            "Kuroe.Flow.Control.Graph",
            "Kuroe.Flow.Node.Shared",
        ];

        Assert.NotEmpty(SourceFiles(presentation));
        foreach (string file in SourceFiles(presentation))
        {
            string text = File.ReadAllText(file);
            foreach (string word in forbidden)
            {
                Assert.False(
                    text.Contains(word, StringComparison.Ordinal),
                    $"{Path.GetFileName(file)} 出现图命名空间 {word}。");
            }
        }
    }

    [Fact]
    public void Flow_control_port_excludes_presentation_and_stop()
    {
        string control = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Shared", "IFlowControl.cs");
        string factory = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Shared", "IFlowControlFactory.cs");
        string controlText = File.ReadAllText(control);

        Assert.DoesNotContain("CaptureView", controlText, StringComparison.Ordinal);
        Assert.DoesNotContain("StopExecution", controlText, StringComparison.Ordinal);
        Assert.DoesNotContain("Attach", File.ReadAllText(factory), StringComparison.Ordinal);
    }

    [Fact]
    public void Graph_state_stores_execution_contexts_not_objects()
    {
        string file = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Control",
            "Graph",
            "Runtime",
            "Graph",
            "GraphFlowState.cs");
        string text = File.ReadAllText(file);

        Assert.DoesNotContain("private readonly List<GraphExecution>", text, StringComparison.Ordinal);
        Assert.DoesNotContain("public List<GraphExecution>", text, StringComparison.Ordinal);
        Assert.Contains("Dictionary<NeutralExecutionId, GraphExecutionContext>", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_implementations_split_ports_by_class()
    {
        string graph = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Graph", "Runtime", "Engine");
        string dialogue = Path.Combine(SourceRoot, "Kuroe", "Assistant", "Core", "Dialogue");

        Assert.False(File.Exists(Path.Combine(graph, "GraphFlowFramework.cs")));
        Assert.False(File.Exists(Path.Combine(dialogue, "DialogueFlowFramework.cs")));
        Assert.True(File.Exists(Path.Combine(graph, "GraphFlowIdentity.cs")));
        Assert.True(File.Exists(Path.Combine(graph, "GraphFlowSubmission.cs")));
        Assert.True(File.Exists(Path.Combine(graph, "GraphFlowStateBinder.cs")));
        Assert.True(File.Exists(Path.Combine(graph, "GraphFlowControlFactory.cs")));
        Assert.True(File.Exists(Path.Combine(graph, "GraphFlowPresentation.cs")));
        Assert.True(File.Exists(Path.Combine(graph, "GraphFlowRecordSource.cs")));
        Assert.True(File.Exists(Path.Combine(dialogue, "DialogueFlowIdentity.cs")));
        Assert.True(File.Exists(Path.Combine(dialogue, "DialogueFlowSubmission.cs")));
        Assert.True(File.Exists(Path.Combine(dialogue, "DialogueFlowControlFactory.cs")));
        Assert.True(File.Exists(Path.Combine(dialogue, "DialogueFlowPresentation.cs")));
        Assert.True(File.Exists(Path.Combine(dialogue, "DialogueFlowRecordSource.cs")));
    }

    [Fact]
    public void Flow_name_lives_in_neutral_tasks()
    {
        string file = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Shared", "FlowName.cs");

        Assert.Contains("namespace Kuroe.Flow.Tasks;", File.ReadAllText(file), StringComparison.Ordinal);
    }

    [Fact]
    public void Execution_records_stay_free_of_graph_and_node_types()
    {
        string records = Path.Combine(SourceRoot, "Kuroe.Flow", "Execution", "Records", "Shared");
        string[] forbidden =
        [
            "NodePath",
            "NodeGraph",
            "ExecutableNode",
            "GraphExecution",
            "RuntimeGraph",
            "StaticGraph",
        ];

        Assert.NotEmpty(SourceFiles(records));
        foreach (string file in SourceFiles(records))
        {
            string text = File.ReadAllText(file);
            foreach (string word in forbidden)
            {
                Assert.False(
                    text.Contains(word, StringComparison.Ordinal),
                    $"{Path.GetFileName(file)} 出现图或节点语义 {word}。");
            }
        }
    }

    [Fact]
    public void Hosts_do_not_reference_graph_runtime_or_node_shared()
    {
        string[] projects =
        [
            Path.Combine(SourceRoot, "Kuroe.Console", "Kuroe.Console.csproj"),
            Path.Combine(SourceRoot, "Kuroe.Cli", "Kuroe.Cli.csproj"),
            Path.Combine(
                SourceRoot,
                "Kuroe",
                "Tool",
                "Implementation",
                "KuroeInfo",
                "Core",
                "Kuroe.Tool.KuroeInfo.csproj"),
        ];

        foreach (string project in projects)
        {
            IReadOnlyList<string> references = ProjectReferences(project);

            Assert.DoesNotContain(references, reference =>
                IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Graph")));
            Assert.DoesNotContain(references, reference =>
                IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow", "Node", "Shared")));
            Assert.DoesNotContain(references, reference =>
                IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow", "Shared")));
        }
    }

    [Fact]
    public void Flow_runtime_references_only_neutral_contracts()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Runtime",
            "Core",
            "Kuroe.Flow.Runtime.csproj");
        string control = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Shared");
        string execution = Path.Combine(SourceRoot, "Kuroe.Flow", "Execution", "Shared");
        string tasks = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Shared");

        Assert.All(
            ProjectReferences(project),
            reference => Assert.True(
                IsUnder(reference, control)
                    || IsUnder(reference, execution)
                    || IsUnder(reference, tasks),
                $"Kuroe.Flow.Runtime 引用了中性契约之外的 {reference}。"));
    }

    [Fact]
    public void Tasks_presentation_references_only_neutral_contracts()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Tasks",
            "Presentation",
            "Shared",
            "Kuroe.Flow.Tasks.Presentation.Shared.csproj");
        string tasks = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Shared");
        string execution = Path.Combine(SourceRoot, "Kuroe.Flow", "Execution", "Shared");
        string presentation = Path.Combine(SourceRoot, "Kuroe.Flow", "Presentation", "Shared");

        Assert.All(
            ProjectReferences(project),
            reference => Assert.True(
                IsUnder(reference, tasks)
                    || IsUnder(reference, execution)
                    || IsUnder(reference, presentation),
                $"Kuroe.Flow.Tasks.Presentation.Shared 引用了中性契约之外的 {reference}。"));
    }

    [Fact]
    public void Host_sources_do_not_import_graph_namespaces()
    {
        string[] directories =
        [
            Path.Combine(SourceRoot, "Kuroe.Console"),
            Path.Combine(SourceRoot, "Kuroe.Cli"),
            Path.Combine(SourceRoot, "Kuroe", "Tool", "Implementation", "KuroeInfo"),
        ];
        string[] forbidden =
        [
            "using Kuroe.Flow.Node.StaticGraph",
            "using Kuroe.Flow.Node.RuntimeGraph",
            "using Kuroe.Flow.Control.Graph",
            "using Kuroe.Flow.Node.Shared",
        ];

        foreach (string directory in directories)
        {
            Assert.NotEmpty(SourceFiles(directory));
            foreach (string file in SourceFiles(directory))
            {
                string text = File.ReadAllText(file);
                foreach (string word in forbidden)
                {
                    Assert.False(
                        text.Contains(word, StringComparison.Ordinal),
                        $"{Path.GetFileName(file)} 引入了图命名空间 {word}。");
                }
            }
        }
    }

    [Fact]
    public void Graph_execution_interface_is_named_IGraphExecution()
    {
        string execution = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Node",
            "Shared",
            "RuntimeGraph",
            "Execution");

        Assert.True(File.Exists(Path.Combine(execution, "IGraphExecution.cs")));
        Assert.False(File.Exists(Path.Combine(execution, "IExecution.cs")));
        Assert.Contains(
            "interface IGraphExecution",
            File.ReadAllText(Path.Combine(execution, "IGraphExecution.cs")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Control_graph_references_only_contract_and_graph_layers()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Control",
            "Graph",
            "Kuroe.Flow.Control.Graph.csproj");
        string control = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Shared");
        string shared = Path.Combine(SourceRoot, "Kuroe.Flow", "Shared");
        string execution = Path.Combine(SourceRoot, "Kuroe.Flow", "Execution", "Shared");
        string executionRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Execution", "Records", "Shared");
        string graphRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Graph", "Records", "Shared");
        string inputChannel = Path.Combine(SourceRoot, "Kuroe.Flow", "InputChannel", "Shared");
        string nodes = Path.Combine(SourceRoot, "Kuroe.Flow", "Node", "Shared");
        string presentation = Path.Combine(SourceRoot, "Kuroe.Flow", "Presentation", "Shared");
        string tasks = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Shared");
        string taskPresentation = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Presentation", "Shared");
        string taskRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Records", "Shared");

        Assert.All(
            ProjectReferences(project),
            reference => Assert.True(
                IsUnder(reference, control)
                    || IsUnder(reference, shared)
                    || IsUnder(reference, execution)
                    || IsUnder(reference, executionRecords)
                    || IsUnder(reference, graphRecords)
                    || IsUnder(reference, inputChannel)
                    || IsUnder(reference, nodes)
                    || IsUnder(reference, presentation)
                    || IsUnder(reference, tasks)
                    || IsUnder(reference, taskPresentation)
                    || IsUnder(reference, taskRecords),
                $"Kuroe.Flow.Control.Graph 引用了契约层与图记录之外的 {reference}。"));
    }

    [Fact]
    public void Execution_shared_defines_no_concrete_state_machine()
    {
        string shared = Path.Combine(SourceRoot, "Kuroe.Flow", "Execution", "Shared");
        string core = Path.Combine(SourceRoot, "Kuroe.Flow", "Execution", "Core");

        Assert.False(File.Exists(Path.Combine(shared, "Execution.cs")));
        Assert.True(File.Exists(Path.Combine(core, "Execution.cs")));
        Assert.All(
            SourceFiles(shared),
            file => Assert.DoesNotContain(
                "sealed class Execution :",
                File.ReadAllText(file),
                StringComparison.Ordinal));
    }

    [Fact]
    public void Flow_control_does_not_name_concrete_execution()
    {
        string[] directories =
        [
            Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Shared"),
            Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Graph"),
            Path.Combine(SourceRoot, "Kuroe", "Assistant", "Core"),
        ];
        string[] forbidden =
        [
            "Kuroe.Flow.Execution.Execution;",
            "Kuroe.Flow.Execution.Execution ",
            "Kuroe.Flow.Execution.Execution)",
            "Kuroe.Flow.Execution.Execution,",
        ];

        foreach (string directory in directories)
        {
            foreach (string file in SourceFiles(directory))
            {
                string text = File.ReadAllText(file);
                foreach (string name in forbidden)
                {
                    Assert.False(
                        text.Contains(name, StringComparison.Ordinal),
                        $"{Path.GetFileName(file)} 引用了执行状态机。");
                }
            }
        }
    }

    [Fact]
    public void Graph_namespaces_follow_their_owning_projects()
    {
        string[] forbidden =
        [
            "Kuroe.Flow.Shared.StaticGraph",
            "Kuroe.Flow.Shared.RuntimeGraph",
            "Kuroe.Flow.Runtime.Graph",
            "Kuroe.Flow.Runtime.Engine",
            "Kuroe.Flow.Runtime.Contract",
            "Kuroe.Flow.Runtime.Nodes",
        ];

        foreach (string file in SourceFiles(SourceRoot))
        {
            string text = File.ReadAllText(file);
            foreach (string word in forbidden)
            {
                Assert.False(
                    text.Contains(word, StringComparison.Ordinal),
                    $"{Path.GetFileName(file)} 出现旧图命名空间 {word}。");
            }
        }
    }

    [Fact]
    public void Work_task_does_not_repeat_internal_state_mutations_as_public_members()
    {
        string file = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Core",
            "Runtime",
            "Tasks",
            "WorkTask.cs");
        string text = File.ReadAllText(file);
        string[] forbidden =
        [
            "public Lock Gate",
            "public void MarkCompleted",
            "public void AppendInfo",
            "public void AppendError",
            "public void AppendJournal",
            "public void RestoreJournal",
            "public void NotifyChanged",
            "public void NotifyExecutionSettled",
            "public void PauseForRestore",
            "public void Report(",
            "public void Touch(",
            "public ErrorOr<Success> Pause(",
            "public ErrorOr<Success> Resume(",
            "public ErrorOr<Success> Terminate(",
            "public void AttachExecution(",
        ];

        foreach (string word in forbidden)
        {
            Assert.DoesNotContain(word, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Task_actions_and_hosts_resolve_execution_ownership_without_parsing_owner_text()
    {
        string[] files =
        [
            Path.Combine(SourceRoot, "Kuroe.Flow", "Core", "Runtime", "Tasks", "TaskActions.cs"),
            Path.Combine(SourceRoot, "Kuroe.Console", "Views", "TaskBrowser.cs"),
            Path.Combine(
                SourceRoot,
                "Kuroe",
                "Tool",
                "Implementation",
                "KuroeInfo",
                "Core",
                "TaskKuroeInfoTool.cs"),
        ];

        foreach (string file in files)
        {
            Assert.DoesNotContain(
                "Request.Owner",
                File.ReadAllText(file),
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Execution_runtime_exposes_control_commands_not_state_transitions()
    {
        string file = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Execution",
            "Shared",
            "IExecutionRuntime.cs");
        string text = File.ReadAllText(file);
        string[] forbidden =
        [
            "MarkRunning",
            "MarkFailed",
            "MarkSucceeded",
            "MarkInterrupted",
            "MarkUncollected",
            "AttachJournal",
            "AttachUsageReader",
            "SettleUsage",
            "AttachRestoreDataReader",
            "SettleRestoreData",
            "ContinueFrom",
            "CreateRunner",
            "CreateRestored",
            "DispatchAsync",
            "Succeed(",
            "Fail(",
        ];

        foreach (string word in forbidden)
        {
            Assert.DoesNotContain(word, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Graph_control_does_not_drive_the_execution_state_machine()
    {
        string directory = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Graph");
        string[] forbidden =
        [
            "MarkRunning",
            "MarkFailed",
            "MarkSucceeded",
            "MarkInterrupted",
            "MarkUncollected",
            "AttachJournal",
            "AttachUsageReader",
            "SettleUsage",
            "AttachRestoreDataReader",
            "SettleRestoreData",
            "CreateRunner",
            "CreateRestored",
        ];

        Assert.NotEmpty(SourceFiles(directory));
        foreach (string file in SourceFiles(directory))
        {
            string text = File.ReadAllText(file);
            foreach (string word in forbidden)
            {
                Assert.False(
                    text.Contains(word, StringComparison.Ordinal),
                    $"{Path.GetFileName(file)} 出现执行状态机写入口 {word}。");
            }
        }
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

    private static IEnumerable<string> SourceFiles(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
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

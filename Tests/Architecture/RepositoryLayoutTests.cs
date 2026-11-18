using System.Xml.Linq;
using Xunit;

namespace Kuroe.Architecture.Tests;

/// <summary>仓库结构的约束：IVT 只指向对应测试程序集，Shared 测试只引用共享源码与工具内核，文件访问 Shared 不引用同模块 Core 与 Kuroe.Files。</summary>
public sealed class RepositoryLayoutTests
{
    private static readonly string Root = FindRoot();
    private static readonly string SourceRoot = Path.Combine(Root, "Source");
    private static readonly string TestsRoot = Path.Combine(Root, "Tests");
    private static readonly string KernelRoot = Path.Combine(SourceRoot, "Kuroe.Kernel");

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
    public void Execution_shared_project_has_no_project_references()
    {
        string project = Path.Combine(
            KernelRoot,
            "Execution",
            "Shared",
            "Kuroe.Execution.Shared.csproj");

        Assert.Empty(ProjectReferences(project));
    }

    [Fact]
    public void Execution_projects_stay_free_of_flow_control_vocabulary()
    {
        string execution = Path.Combine(KernelRoot, "Execution");
        string pattern = @"(?<![A-Za-z])(FlowStepPath|StartInputs|FlowName|FlowSeed|PortName)(?![A-Za-z])";

        Assert.NotEmpty(SourceFiles(execution));
        foreach (string file in SourceFiles(execution))
        {
            string text = File.ReadAllText(file);

            Assert.False(
                System.Text.RegularExpressions.Regex.IsMatch(text, pattern),
                $"{Path.GetFileName(file)} 使用了流程控制词汇。");
            Assert.DoesNotContain("Kuroe.Tasks", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Kuroe.Flow.Node", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Kuroe.Flow.Control", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Kuroe.Control", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Flow_shared_project_is_a_pure_model()
    {
        string shared = Path.Combine(SourceRoot, "Kuroe.Flow", "Shared");
        string project = Path.Combine(shared, "Kuroe.Flow.Shared.csproj");

        Assert.Empty(ProjectReferences(project));
        Assert.False(File.Exists(Path.Combine(shared, "GlobalUsings.cs")));

        foreach (string file in SourceFiles(shared))
        {
            string text = File.ReadAllText(file);

            Assert.DoesNotContain("using Kuroe.Execution", text, StringComparison.Ordinal);
            Assert.DoesNotContain("using Kuroe.Tasks", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Presentation_shared_project_stays_free_of_graph_and_control_references()
    {
        string project = Path.Combine(
            KernelRoot,
            "Presentation",
            "Shared",
            "Kuroe.Presentation.Shared.csproj");

        string execution = Path.Combine(KernelRoot, "Execution", "Shared");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");

        Assert.All(
            ProjectReferences(project),
            reference => Assert.True(
                IsUnder(reference, execution) || IsUnder(reference, tasks),
                $"Kuroe.Presentation.Shared 引用了中性契约之外的 {reference}。"));
    }

    [Fact]
    public void Execution_core_references_only_execution_contracts()
    {
        string shared = Path.Combine(
            KernelRoot,
            "Execution",
            "Shared",
            "Kuroe.Execution.Shared.csproj");
        string core = Path.Combine(
            KernelRoot,
            "Execution",
            "Core",
            "Kuroe.Execution.csproj");

        Assert.Equal([shared], ProjectReferences(core));
    }

    [Fact]
    public void Flow_hosting_does_not_reference_execution_core()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Hosting",
            "Core",
            "Kuroe.Flow.Hosting.csproj");
        string execution = Path.Combine(KernelRoot, "Execution", "Core");

        Assert.DoesNotContain(
            ProjectReferences(project),
            reference => IsUnder(reference, execution));
    }

    [Fact]
    public void Execution_projects_stay_free_of_graph_and_node_semantics()
    {
        string execution = Path.Combine(KernelRoot, "Execution");
        string[] forbidden =
        [
            "NodePath",
            "NodeGraph",
            "ExecutableNode",
            "ModelPort",
            "GraphExecution",
            "IExecutionNodeDeclarationResolver",
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
            "Kuroe.Kernel",
            "Control",
            "Runtime",
            "FlowRuntime.cs");
        string text = File.ReadAllText(file);

        foreach (string word in (string[])["GraphFlowControl", "TaskRuntime", "GraphExecution", "NodeGraph"])
        {
            Assert.DoesNotContain(word, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Flow_runtime_lives_in_its_own_project()
    {
        string runtime = Path.Combine(SourceRoot, "Kuroe.Kernel", "Control", "Runtime", "FlowRuntime.cs");
        string project = Path.Combine(SourceRoot, "Kuroe.Kernel", "Control", "Runtime", "Kuroe.Control.Runtime.csproj");
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
        string project = Path.Combine(SourceRoot, "Kuroe.Kernel", "Tasks", "Runtime", "Kuroe.Tasks.Runtime.csproj");

        Assert.DoesNotContain(
            ProjectReferences(project),
            reference => IsUnder(reference, Path.Combine(KernelRoot, "Execution", "Core")));
    }

    [Fact]
    public void Control_shared_stays_free_of_graph_runtime()
    {
        string control = Path.Combine(KernelRoot, "Control", "Shared");
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
        string project = Path.Combine(SourceRoot, "Kuroe.Kernel", "Tasks", "Runtime", "Kuroe.Tasks.Runtime.csproj");
        string graph = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Graph");

        Assert.DoesNotContain(ProjectReferences(project), reference => IsUnder(reference, graph));
    }

    [Fact]
    public void Core_source_stays_free_of_graph_runtime()
    {
        string core = Path.Combine(SourceRoot, "Kuroe.Kernel", "Tasks", "Runtime");
        string[] forbidden =
        [
            "GraphFlowControl",
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
        string core = Path.Combine(SourceRoot, "Kuroe.Kernel", "Tasks", "Runtime");
        string graph = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Graph");

        Assert.True(File.Exists(Path.Combine(core, "WorkTask.cs")));
        Assert.False(File.Exists(Path.Combine(graph, "Runtime", "Tasks", "WorkTask.cs")));
        Assert.True(File.Exists(Path.Combine(graph, "Runtime", "Engine", "GraphFlowControl.cs")));
        Assert.True(File.Exists(Path.Combine(graph, "Runtime", "Graph", "TaskRuntime.cs")));
    }

    [Fact]
    public void Task_registry_has_no_restorer_surface()
    {
        string file = Path.Combine(
            SourceRoot,
            "Kuroe.Kernel",
            "Tasks",
            "Runtime",
            "TaskRegistry.cs");

        Assert.DoesNotContain("Restorers", File.ReadAllText(file), StringComparison.Ordinal);
    }

    [Fact]
    public void Node_projects_do_not_reference_flow_implementation()
    {
        string nodes = Path.Combine(SourceRoot, "Kuroe", "Node");
        string execution = Path.Combine(KernelRoot, "Execution", "Core");
        string flow = Path.Combine(SourceRoot, "Kuroe.Kernel", "Tasks", "Runtime");
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
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string execution = Path.Combine(KernelRoot, "Execution", "Shared");
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
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string execution = Path.Combine(KernelRoot, "Execution", "Shared");

        Assert.All(
            ProjectReferences(project),
            reference => Assert.True(
                IsUnder(reference, tasks) || IsUnder(reference, execution),
                $"Kuroe.Flow.Shared 引用了中性共享之外的 {reference}。"));
    }

    [Fact]
    public void Task_presentation_stays_free_of_graph_control()
    {
        string presentation = Path.Combine(KernelRoot, "Tasks", "Presentation", "Shared");
        string[] forbidden = ["GraphFlowControl", "TaskRuntime", "NodeGraph", "Control.Graph"];

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
        string presentation = Path.Combine(KernelRoot, "Tasks", "Presentation", "Shared");
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
            "Kuroe.Kernel",
            "Control",
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
            KernelRoot,
            "Control",
            "Shared",
            "IFlowExecutionGateway.cs");

        Assert.DoesNotContain("Attach", File.ReadAllText(file), StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_restore_resolves_by_identity()
    {
        string framework = Path.Combine(
            KernelRoot,
            "Control",
            "Shared",
            "IFlowIdentity.cs");
        string contract = Path.Combine(
            KernelRoot,
            "Control",
            "Shared",
            "IFlowProviderSource.cs");
        string source = Path.Combine(
            SourceRoot,
            "Kuroe.Kernel",
            "Tasks",
            "Runtime",
            "FlowProviderSource.cs");
        string restore = Path.Combine(
            SourceRoot,
            "Kuroe.Kernel",
            "Tasks",
            "Runtime",
            "Persistence",
            "TaskRestoreRuntime.cs");

        Assert.Contains("Handles(FlowIdentity identity)", File.ReadAllText(framework), StringComparison.Ordinal);
        Assert.DoesNotContain("FlowRecord", File.ReadAllText(framework), StringComparison.Ordinal);
        Assert.Contains("Find(FlowIdentity identity)", File.ReadAllText(contract), StringComparison.Ordinal);
        Assert.Contains("Find(record.Flow.Identity)", File.ReadAllText(restore), StringComparison.Ordinal);
        Assert.Contains("Handles(identity)", File.ReadAllText(source), StringComparison.Ordinal);
    }

    [Fact]
    public void Task_submission_has_no_delegate_entry()
    {
        string actions = Path.Combine(
            SourceRoot,
            "Kuroe.Kernel",
            "Tasks",
            "Runtime",
            "TaskActions.cs");
        string text = File.ReadAllText(actions);

        Assert.DoesNotContain("Func<TaskId, ErrorOr<FlowSubmission>>", text, StringComparison.Ordinal);
        Assert.Contains("provider.Submission.Prepare", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Task_shared_project_stays_free_of_graph_references()
    {
        string project = Path.Combine(
            KernelRoot,
            "Tasks",
            "Shared",
            "Kuroe.Tasks.Shared.csproj");
        IReadOnlyList<string> references = ProjectReferences(project);

        Assert.DoesNotContain(references, reference =>
            IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow", "Shared")));
        Assert.DoesNotContain(references, reference =>
            IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Kernel", "Tasks", "Runtime")));
        Assert.DoesNotContain(references, reference =>
            IsUnder(reference, Path.Combine(KernelRoot, "Execution")));
    }

    [Fact]
    public void Execution_records_project_references_only_execution_shared()
    {
        string project = Path.Combine(
            KernelRoot,
            "Execution",
            "Records",
            "Shared",
            "Kuroe.Execution.Records.Shared.csproj");

        IReadOnlyList<string> references = ProjectReferences(project);

        Assert.Single(references);
        Assert.Contains(
            Path.Combine(KernelRoot, "Execution", "Shared", "Kuroe.Execution.Shared.csproj"),
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
    public void Task_records_project_references_only_task_and_execution()
    {
        string project = Path.Combine(
            KernelRoot,
            "Tasks",
            "Records",
            "Shared",
            "Kuroe.Tasks.Records.Shared.csproj");
        IReadOnlyList<string> references = ProjectReferences(project);
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string executionRecords = Path.Combine(KernelRoot, "Execution", "Records", "Shared");
        string execution = Path.Combine(KernelRoot, "Execution", "Shared");

        Assert.All(references, reference =>
            Assert.True(
                IsUnder(reference, tasks)
                    || IsUnder(reference, executionRecords)
                    || IsUnder(reference, execution),
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
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string tasksRecords = Path.Combine(KernelRoot, "Tasks", "Records", "Shared");
        string execution = Path.Combine(KernelRoot, "Execution", "Shared");
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
        string persistence = Path.Combine(KernelRoot, "Tasks", "Persistence");
        string taskContracts = Path.Combine(KernelRoot, "Tasks", "Shared");
        string taskPresentation = Path.Combine(KernelRoot, "Tasks", "Presentation", "Shared");
        string executionContracts = Path.Combine(KernelRoot, "Execution", "Shared");
        string executionRecords = Path.Combine(KernelRoot, "Execution", "Records", "Shared");
        string taskRecords = Path.Combine(KernelRoot, "Tasks", "Records", "Shared");

        foreach (string project in ProjectFiles(persistence))
        {
            IReadOnlyList<string> references = [.. ProjectReferences(project)
                .Where(reference => IsUnder(reference, SourceRoot))];

            Assert.All(references, reference =>
                Assert.True(
                    IsPersistenceBoundary(reference, persistence)
                        || IsUnder(reference, taskContracts)
                        || IsUnder(reference, taskPresentation)
                        || IsUnder(reference, executionContracts)
                        || IsUnder(reference, executionRecords)
                        || IsUnder(reference, taskRecords),
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
    public void Persistence_task_projects_stay_free_of_graph_and_node()
    {
        string persistence = Path.Combine(KernelRoot, "Tasks", "Persistence");
        string[] projects =
        [
            Path.Combine(persistence, "Files", "Shared", "Kuroe.Tasks.Persistence.Files.Shared.csproj"),
            Path.Combine(persistence, "Files", "Core", "Kuroe.Tasks.Persistence.Files.csproj"),
            Path.Combine(persistence, "Restoring", "Shared", "Kuroe.Tasks.Persistence.Restoring.Shared.csproj"),
            Path.Combine(persistence, "Restoring", "Core", "Kuroe.Tasks.Persistence.Restoring.csproj"),
            Path.Combine(persistence, "Saving", "Shared", "Kuroe.Tasks.Persistence.Saving.Shared.csproj"),
            Path.Combine(persistence, "Saving", "Core", "Kuroe.Tasks.Persistence.Saving.csproj"),
        ];
        string graphRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Graph", "Records", "Shared");
        string nodeContracts = Path.Combine(SourceRoot, "Kuroe.Flow", "Node", "Shared");
        string flowContracts = Path.Combine(SourceRoot, "Kuroe.Flow", "Shared");

        foreach (string project in projects)
        {
            Assert.All(ProjectReferences(project), reference => Assert.False(
                IsUnder(reference, graphRecords) || IsUnder(reference, nodeContracts) || IsUnder(reference, flowContracts),
                $"{Path.GetFileName(project)} 引用了图或节点模型 {reference}。"));

            foreach (string file in SourceFiles(DirectoryOf(project)))
            {
                string text = File.ReadAllText(file);
                Assert.DoesNotContain("Kuroe.Flow.Node", text, StringComparison.Ordinal);
                Assert.DoesNotContain("Kuroe.Flow.Graph", text, StringComparison.Ordinal);
                Assert.DoesNotContain("Kuroe.Flow.Shared", text, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Serialization_shared_and_json_stay_free_of_graph_and_node()
    {
        string persistence = Path.Combine(KernelRoot, "Tasks", "Persistence");
        string[] projects =
        [
            Path.Combine(persistence, "Serialization", "Shared", "Kuroe.Tasks.Persistence.Serialization.Shared.csproj"),
            Path.Combine(persistence, "Serialization", "Json", "Core", "Kuroe.Tasks.Persistence.Json.csproj"),
        ];
        string graphRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Graph", "Records", "Shared");
        string nodeContracts = Path.Combine(SourceRoot, "Kuroe.Flow", "Node", "Shared");
        string flowContracts = Path.Combine(SourceRoot, "Kuroe.Flow", "Shared");

        foreach (string project in projects)
        {
            Assert.All(ProjectReferences(project), reference => Assert.False(
                IsUnder(reference, graphRecords) || IsUnder(reference, nodeContracts) || IsUnder(reference, flowContracts),
                $"{Path.GetFileName(project)} 引用了图或节点模型 {reference}。"));

            foreach (string file in SourceFiles(DirectoryOf(project)))
            {
                string text = File.ReadAllText(file);
                Assert.DoesNotContain("Kuroe.Flow.Node", text, StringComparison.Ordinal);
                Assert.DoesNotContain("Kuroe.Flow.Graph", text, StringComparison.Ordinal);
                Assert.DoesNotContain("Kuroe.Flow.Shared", text, StringComparison.Ordinal);
                Assert.DoesNotContain("GraphFlowDefinitionRecord", text, StringComparison.Ordinal);
                Assert.DoesNotContain("CodeFlowDefinitionRecord", text, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Flow_record_codecs_live_outside_central_serialization()
    {
        string persistence = Path.Combine(KernelRoot, "Tasks", "Persistence", "Serialization");
        string graphCodec = Path.Combine(SourceRoot, "Kuroe.Flow", "Persistence", "Serialization", "Graph", "Core");
        string code = Path.Combine(SourceRoot, "Kuroe.CodeFlow", "Persistence", "Core");

        Assert.True(File.Exists(Path.Combine(graphCodec, "GraphFlowRecordCodec.cs")));
        Assert.True(File.Exists(Path.Combine(code, "CodeFlowRecordCodec.cs")));
        Assert.False(File.Exists(Path.Combine(persistence, "Shared", "FlowRecordCodec.cs")));
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
            KernelRoot,
            "Control",
            "Shared",
            "Kuroe.Control.Shared.csproj");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string tasksPresentation = Path.Combine(KernelRoot, "Tasks", "Presentation", "Shared");
        string tasksRecords = Path.Combine(KernelRoot, "Tasks", "Records", "Shared");
        string execution = Path.Combine(KernelRoot, "Execution", "Shared");
        string presentation = Path.Combine(KernelRoot, "Presentation", "Shared");

        Assert.All(
            ProjectReferences(project),
            reference => Assert.True(
                IsUnder(reference, tasks)
                    || IsUnder(reference, tasksPresentation)
                    || IsUnder(reference, tasksRecords)
                    || IsUnder(reference, execution)
                    || IsUnder(reference, presentation),
                $"Kuroe.Control.Shared 引用了中性契约之外的 {reference}。"));

        Assert.DoesNotContain(
            ProjectReferences(project),
            reference => IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow")));
    }

    [Fact]
    public void Flow_area_stays_free_of_task_and_presentation_projects()
    {
        Assert.False(Directory.Exists(Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks")));
        Assert.False(Directory.Exists(Path.Combine(SourceRoot, "Kuroe.Flow", "Presentation")));
    }

    [Fact]
    public void Control_shared_uses_only_neutral_namespaces()
    {
        string control = Path.Combine(KernelRoot, "Control", "Shared");
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
    public void Task_presentation_uses_only_neutral_namespaces()
    {
        string presentation = Path.Combine(KernelRoot, "Tasks", "Presentation", "Shared");
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
        string control = Path.Combine(KernelRoot, "Control", "Shared", "IFlowControl.cs");
        string factory = Path.Combine(KernelRoot, "Control", "Shared", "IFlowControlFactory.cs");
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
        Assert.Contains("Dictionary<ExecutionId, GraphExecutionContext>", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_implementations_split_ports_by_class()
    {
        string graph = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Graph", "Runtime", "Engine");
        string dialogue = Path.Combine(SourceRoot, "Kuroe", "Assistant", "Core", "Dialogue", "Flow");

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
    public void Assistant_separates_dialogue_hosting_from_its_code_flow()
    {
        string core = Path.Combine(SourceRoot, "Kuroe", "Assistant", "Core");
        string dialogue = Path.Combine(core, "Dialogue");
        string flow = Path.Combine(dialogue, "Flow");

        Assert.True(File.Exists(Path.Combine(dialogue, "DialogueHost.cs")));
        Assert.True(File.Exists(Path.Combine(dialogue, "DialogueRound.cs")));
        Assert.False(File.Exists(Path.Combine(dialogue, "DialogueFlowControl.cs")));
        Assert.True(File.Exists(Path.Combine(flow, "DialogueFlow.cs")));
        Assert.True(File.Exists(Path.Combine(flow, "DialogueFlowControl.cs")));
        Assert.True(File.Exists(Path.Combine(flow, "DialogueFlowView.cs")));
        Assert.False(File.Exists(Path.Combine(flow, "DialogueFlowTarget.cs")));
        Assert.False(Directory.Exists(Path.Combine(core, "Flows")));
    }

    [Fact]
    public void Task_snapshots_are_assembled_by_the_task_domain()
    {
        string control = Path.Combine(KernelRoot, "Control", "Shared");
        string factory = Path.Combine(control, "TaskSnapshotFactory.cs");
        string graph = Path.Combine(
            SourceRoot, "Kuroe.Flow", "Control", "Graph", "Runtime", "Engine", "GraphFlowPresentation.cs");
        string dialogue = Path.Combine(
            SourceRoot, "Kuroe", "Assistant", "Core", "Dialogue", "Flow", "DialogueFlowPresentation.cs");
        string workTask = Path.Combine(
            SourceRoot, "Kuroe.Kernel", "Tasks", "Runtime", "WorkTask.cs");

        Assert.True(File.Exists(factory));
        Assert.Contains("TaskSnapshotFactory", File.ReadAllText(workTask), StringComparison.Ordinal);
        Assert.DoesNotContain("new TaskSnapshot", File.ReadAllText(graph), StringComparison.Ordinal);
        Assert.DoesNotContain("new TaskSnapshot", File.ReadAllText(dialogue), StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_ports_expose_views_pending_steps_and_flow_records()
    {
        string presentation = Path.Combine(
            KernelRoot, "Control", "Shared", "IFlowPresentation.cs");
        string records = Path.Combine(
            KernelRoot, "Control", "Shared", "IFlowRecordSource.cs");
        string graphRecords = Path.Combine(
            SourceRoot, "Kuroe.Flow", "Control", "Graph", "Runtime", "Engine", "GraphFlowRecordSource.cs");
        string dialogueRecords = Path.Combine(
            SourceRoot, "Kuroe", "Assistant", "Core", "Dialogue", "Flow", "DialogueFlowRecordSource.cs");

        Assert.DoesNotContain("CreateSnapshot", File.ReadAllText(presentation), StringComparison.Ordinal);
        Assert.Contains(
            "Pending",
            File.ReadAllText(Path.Combine(KernelRoot, "Control", "Shared", "ITaskFlowView.cs")),
            StringComparison.Ordinal);
        Assert.DoesNotContain("CaptureExecutions", File.ReadAllText(records), StringComparison.Ordinal);
        Assert.DoesNotContain("CaptureExecutions", File.ReadAllText(graphRecords), StringComparison.Ordinal);
        Assert.DoesNotContain("CaptureExecutions", File.ReadAllText(dialogueRecords), StringComparison.Ordinal);
    }

    [Fact]
    public void Execution_records_capture_the_source_key()
    {
        string capture = Path.Combine(
            KernelRoot, "Execution", "Records", "Shared", "ExecutionRecordCapture.cs");

        Assert.Contains("SourceKey", File.ReadAllText(capture), StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_name_lives_in_neutral_tasks()
    {
        string file = Path.Combine(KernelRoot, "Tasks", "Shared", "FlowName.cs");

        Assert.Contains("namespace Kuroe.Tasks.Shared;", File.ReadAllText(file), StringComparison.Ordinal);
    }

    [Fact]
    public void Execution_records_stay_free_of_graph_and_node_types()
    {
        string records = Path.Combine(KernelRoot, "Execution", "Records", "Shared");
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
            Path.Combine(SourceRoot, "Kuroe", "Assistant", "Core", "Kuroe.Assistant.csproj"),
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
            "Kuroe.Kernel",
            "Control",
            "Runtime",
            "Kuroe.Control.Runtime.csproj");
        string control = Path.Combine(KernelRoot, "Control", "Shared");
        string execution = Path.Combine(KernelRoot, "Execution", "Shared");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string taskPresentation = Path.Combine(KernelRoot, "Tasks", "Presentation", "Shared");
        string presentation = Path.Combine(KernelRoot, "Presentation", "Shared");

        Assert.All(
            ProjectReferences(project),
            reference => Assert.True(
                IsUnder(reference, control)
                    || IsUnder(reference, execution)
                    || IsUnder(reference, tasks)
                    || IsUnder(reference, taskPresentation)
                    || IsUnder(reference, presentation),
                $"Kuroe.Control.Runtime 引用了中性契约之外的 {reference}。"));
    }

    [Fact]
    public void Task_presentation_references_only_neutral_contracts()
    {
        string project = Path.Combine(
            KernelRoot,
            "Tasks",
            "Presentation",
            "Shared",
            "Kuroe.Tasks.Presentation.Shared.csproj");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string execution = Path.Combine(KernelRoot, "Execution", "Shared");
        string presentation = Path.Combine(KernelRoot, "Presentation", "Shared");

        Assert.All(
            ProjectReferences(project),
            reference => Assert.True(
                IsUnder(reference, tasks)
                    || IsUnder(reference, execution)
                    || IsUnder(reference, presentation),
                $"Kuroe.Tasks.Presentation.Shared 引用了中性契约之外的 {reference}。"));
    }

    [Fact]
    public void Host_sources_do_not_import_graph_namespaces()
    {
        string[] directories =
        [
            Path.Combine(SourceRoot, "Kuroe.Console"),
            Path.Combine(SourceRoot, "Kuroe.Cli"),
            Path.Combine(SourceRoot, "Kuroe", "Assistant", "Core"),
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
    public void Code_flow_sources_do_not_import_graph_namespaces()
    {
        string[] directories =
        [
            Path.Combine(SourceRoot, "Kuroe", "Assistant", "Core", "Dialogue"),
            Path.Combine(SourceRoot, "Kuroe", "Assistant", "Core", "Dialogue", "Flow"),
        ];
        string[] forbidden =
        [
            "Kuroe.Flow.Node.StaticGraph",
            "Kuroe.Flow.Node.RuntimeGraph",
            "Kuroe.Flow.Node.Shared",
            "Kuroe.Flow.Control.Graph",
            "Kuroe.Flow.Graph.Records",
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
    public void Graph_sources_do_not_redefine_execution_identity_or_state()
    {
        string execution = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Node",
            "Shared",
            "RuntimeGraph",
            "Execution");
        string[] duplicateFiles =
        [
            "ExecutionId.cs",
            "ExecutionState.cs",
            "ExecutionStates.cs",
            "ExecutionOutput.cs",
            "ExecutionOutputKind.cs",
            "ExecutionRestorePayload.cs",
        ];

        foreach (string file in duplicateFiles)
        {
            Assert.False(
                File.Exists(Path.Combine(execution, file)),
                $"{file} 与执行单元重复定义。");
        }

        string[] forbidden =
        [
            "record struct ExecutionId",
            "enum ExecutionState",
            "class ExecutionStates",
            "record ExecutionOutput",
            "enum ExecutionOutputKind",
            "record ExecutionRestorePayload",
        ];
        string[] directories =
        [
            Path.Combine(SourceRoot, "Kuroe.Flow", "Node"),
            Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Graph"),
        ];

        foreach (string directory in directories)
        {
            foreach (string file in SourceFiles(directory))
            {
                string text = File.ReadAllText(file);
                foreach (string word in forbidden)
                {
                    Assert.False(
                        text.Contains(word, StringComparison.Ordinal),
                        $"{Path.GetFileName(file)} 重新定义了 {word}。");
                }
            }
        }
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
        string control = Path.Combine(KernelRoot, "Control", "Shared");
        string shared = Path.Combine(SourceRoot, "Kuroe.Flow", "Shared");
        string execution = Path.Combine(KernelRoot, "Execution", "Shared");
        string executionRecords = Path.Combine(KernelRoot, "Execution", "Records", "Shared");
        string graphRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Graph", "Records", "Shared");
        string nodes = Path.Combine(SourceRoot, "Kuroe.Flow", "Node", "Shared");
        string presentation = Path.Combine(KernelRoot, "Presentation", "Shared");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string taskRecords = Path.Combine(KernelRoot, "Tasks", "Records", "Shared");

        Assert.All(
            ProjectReferences(project),
            reference => Assert.True(
                IsUnder(reference, control)
                    || IsUnder(reference, shared)
                    || IsUnder(reference, execution)
                    || IsUnder(reference, executionRecords)
                    || IsUnder(reference, graphRecords)
                    || IsUnder(reference, nodes)
                    || IsUnder(reference, presentation)
                    || IsUnder(reference, tasks)
                    || IsUnder(reference, taskRecords),
                $"Kuroe.Flow.Control.Graph 引用了契约层与图记录之外的 {reference}。"));
    }

    [Fact]
    public void Execution_shared_defines_no_concrete_state_machine()
    {
        string shared = Path.Combine(KernelRoot, "Execution", "Shared");
        string core = Path.Combine(KernelRoot, "Execution", "Core");

        Assert.False(File.Exists(Path.Combine(shared, "ExecutionInstance.cs")));
        Assert.True(File.Exists(Path.Combine(core, "ExecutionInstance.cs")));
        Assert.All(
            SourceFiles(shared),
            file => Assert.DoesNotContain(
                "sealed class ExecutionInstance :",
                File.ReadAllText(file),
                StringComparison.Ordinal));
    }

    [Fact]
    public void Flow_control_does_not_name_concrete_execution()
    {
        string[] directories =
        [
            Path.Combine(KernelRoot, "Control", "Shared"),
            Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Graph"),
            Path.Combine(SourceRoot, "Kuroe", "Assistant", "Core"),
        ];
        string[] forbidden =
        [
            "Kuroe.Execution.ExecutionInstance;",
            "Kuroe.Execution.ExecutionInstance ",
            "Kuroe.Execution.ExecutionInstance)",
            "Kuroe.Execution.ExecutionInstance,",
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
            "Kuroe.Control.Runtime.Graph",
            "Kuroe.Control.Runtime.Engine",
            "Kuroe.Control.Runtime.Contract",
            "Kuroe.Control.Runtime.Nodes",
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
            "Kuroe.Kernel",
            "Tasks",
            "Runtime",
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
            Path.Combine(SourceRoot, "Kuroe.Kernel", "Tasks", "Runtime", "TaskActions.cs"),
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
            KernelRoot,
            "Execution",
            "Shared",
            "Runtime",
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
    public void Execution_dispatcher_only_schedules_runs()
    {
        string core = Path.Combine(KernelRoot, "Execution", "Core");
        string dispatcher = File.ReadAllText(Path.Combine(core, "ExecutionDispatcher.cs"));
        string run = Path.Combine(core, "ExecutionRun.cs");

        Assert.True(File.Exists(run));
        foreach (string word in (string[])["AttachJournal", "IExecutionUsageSource", "IExecutionRestoreDataSource", "SettleUsage", "SettleRestoreData"])
        {
            Assert.DoesNotContain(word, dispatcher, StringComparison.Ordinal);
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

    [Fact]
    public void Flow_control_port_excludes_input_delivery()
    {
        string control = Path.Combine(KernelRoot, "Control", "Shared", "IFlowControl.cs");
        string runtime = Path.Combine(KernelRoot, "Control", "Shared", "IFlowRuntime.cs");
        string channel = Path.Combine(KernelRoot, "Input", "Shared", "IInputChannel.cs");
        string delivery = Path.Combine(KernelRoot, "Input", "Shared", "IInputDelivery.cs");
        string controlText = File.ReadAllText(control);

        Assert.DoesNotContain("Pending", controlText, StringComparison.Ordinal);
        Assert.DoesNotContain("Deliver", controlText, StringComparison.Ordinal);
        Assert.DoesNotContain("Accept", controlText, StringComparison.Ordinal);
        Assert.DoesNotContain("Advance", File.ReadAllText(runtime), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(KernelRoot, "Control", "Shared", "IFlowAdvance.cs")));
        Assert.Contains("Accept", File.ReadAllText(channel), StringComparison.Ordinal);
        Assert.Contains("Decline", File.ReadAllText(channel), StringComparison.Ordinal);
        Assert.Contains("Pending", File.ReadAllText(delivery), StringComparison.Ordinal);
        Assert.Contains("Deliver", File.ReadAllText(delivery), StringComparison.Ordinal);
        Assert.False(
            File.Exists(Path.Combine(KernelRoot, "Control", "Shared", "IFlowInputControl.cs")));
    }

    [Fact]
    public void Input_projects_reference_only_kernel_contracts()
    {
        string input = Path.Combine(KernelRoot, "Input");
        string shared = Path.Combine(input, "Shared", "Kuroe.Input.Shared.csproj");
        string runtime = Path.Combine(input, "Runtime", "Kuroe.Input.Runtime.csproj");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string sharedProject = Path.Combine(input, "Shared");

        Assert.Equal([Path.Combine(tasks, "Kuroe.Tasks.Shared.csproj")], ProjectReferences(shared));
        Assert.Equal(
            [Path.Combine(sharedProject, "Kuroe.Input.Shared.csproj"), Path.Combine(tasks, "Kuroe.Tasks.Shared.csproj")],
            ProjectReferences(runtime));
    }

    [Fact]
    public void Input_projects_stay_free_of_flow_implementation()
    {
        string input = Path.Combine(KernelRoot, "Input");
        string[] forbidden =
        [
            Path.Combine(SourceRoot, "Kuroe.Flow"),
            Path.Combine(SourceRoot, "Kuroe", "Node"),
            Path.Combine(KernelRoot, "Execution"),
            Path.Combine(KernelRoot, "Control"),
            Path.Combine(KernelRoot, "Tasks", "Runtime"),
        ];

        Assert.NotEmpty(ProjectFiles(input));
        foreach (string project in ProjectFiles(input))
        {
            Assert.DoesNotContain(
                ProjectReferences(project),
                reference => forbidden.Any(directory => IsUnder(reference, directory)));
        }
    }

    [Fact]
    public void Input_projects_stay_free_of_graph_and_node_vocabulary()
    {
        string input = Path.Combine(KernelRoot, "Input");
        string pattern = @"(?<![A-Za-z])(Graph|Node)(?![A-Za-z])";

        Assert.NotEmpty(SourceFiles(input));
        foreach (string file in SourceFiles(input))
        {
            Assert.False(
                System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(file), pattern),
                $"{Path.GetFileName(file)} 出现图或节点词汇。");
        }
    }

    [Fact]
    public void Node_projects_reference_their_runner_implementation()
    {
        string runnerShared = Path.Combine(
            SourceRoot, "Kuroe", "Runner", "Shared", "Kuroe.Runner.Shared.csproj");
        string runnerRoot = Path.Combine(SourceRoot, "Kuroe", "Runner");
        (string Project, string Runner, bool AllowsRunnerShared)[] projects =
        [
            (
                Path.Combine(SourceRoot, "Kuroe", "Node", "Input", "Core", "Kuroe.Node.Input.csproj"),
                Path.Combine(runnerRoot, "Input", "Core", "Kuroe.Runner.Input.csproj"),
                false),
            (
                Path.Combine(SourceRoot, "Kuroe", "Node", "Approval", "Core", "Kuroe.Node.Approval.csproj"),
                Path.Combine(runnerRoot, "Approval", "Core", "Kuroe.Runner.Approval.csproj"),
                false),
            (
                Path.Combine(SourceRoot, "Kuroe", "Node", "Command", "Core", "Kuroe.Node.Command.csproj"),
                Path.Combine(runnerRoot, "Command", "Core", "Kuroe.Runner.Command.csproj"),
                false),
            (
                Path.Combine(SourceRoot, "Kuroe", "Node", "File", "Core", "Kuroe.Node.File.csproj"),
                Path.Combine(runnerRoot, "File", "Core", "Kuroe.Runner.File.csproj"),
                false),
            (
                Path.Combine(SourceRoot, "Kuroe", "Node", "Llm", "Core", "Kuroe.Node.Llm.csproj"),
                Path.Combine(runnerRoot, "Llm", "Core", "Kuroe.Runner.Llm.csproj"),
                true),
        ];

        foreach ((string project, string runner, bool allowsRunnerShared) in projects)
        {
            IReadOnlyList<string> references = ProjectReferences(project);

            Assert.Contains(runner, references);
            Assert.DoesNotContain(references, reference =>
                IsUnder(reference, runnerRoot)
                && !string.Equals(reference, runner, StringComparison.OrdinalIgnoreCase)
                && !(allowsRunnerShared && string.Equals(reference, runnerShared, StringComparison.OrdinalIgnoreCase)));
        }
    }

    [Fact]
    public void Runner_projects_stay_free_of_node_and_flow()
    {
        Assert.False(Directory.Exists(Path.Combine(SourceRoot, "Kuroe.Runner")));
        string runner = Path.Combine(SourceRoot, "Kuroe", "Runner");
        string[] forbidden =
        [
            Path.Combine(SourceRoot, "Kuroe", "Node"),
            Path.Combine(SourceRoot, "Kuroe.Flow"),
            Path.Combine(KernelRoot, "Control"),
            Path.Combine(KernelRoot, "Tasks", "Runtime"),
        ];

        Assert.NotEmpty(ProjectFiles(runner));
        foreach (string project in ProjectFiles(runner))
        {
            Assert.DoesNotContain(
                ProjectReferences(project),
                reference => forbidden.Any(directory => IsUnder(reference, directory)));
        }
    }

    [Fact]
    public void Process_project_is_a_neutral_capability()
    {
        Assert.False(Directory.Exists(Path.Combine(SourceRoot, "Kuroe", "Process")));

        string project = Path.Combine(SourceRoot, "Kuroe.Process", "Core", "Kuroe.Process.csproj");
        Assert.True(File.Exists(project));
        Assert.Empty(ProjectReferences(project));
    }

    [Fact]
    public void Runner_shared_references_only_neutral_contracts()
    {
        string project = Path.Combine(SourceRoot, "Kuroe", "Runner", "Shared", "Kuroe.Runner.Shared.csproj");

        Assert.Empty(ProjectReferences(project));
    }

    [Fact]
    public void Execution_kind_is_removed()
    {
        string[] roots =
        [
            SourceRoot,
            TestsRoot,
        ];

        string removed = "Execution" + "Kind";
        foreach (string file in roots.SelectMany(SourceFiles))
        {
            Assert.DoesNotContain(
                removed,
                File.ReadAllText(file),
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Graph_control_does_not_reference_input()
    {
        string project = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Graph", "Kuroe.Flow.Control.Graph.csproj");
        string input = Path.Combine(KernelRoot, "Input");

        Assert.DoesNotContain(ProjectReferences(project), reference => IsUnder(reference, input));
    }

    [Fact]
    public void Flow_control_port_excludes_process_specific_commands()
    {
        string control = Path.Combine(KernelRoot, "Control", "Shared", "IFlowControl.cs");
        string rerun = Path.Combine(KernelRoot, "Control", "Shared", "IFlowRerun.cs");
        string controlText = File.ReadAllText(control);

        Assert.DoesNotContain("Advance", controlText, StringComparison.Ordinal);
        Assert.DoesNotContain("Rerun", controlText, StringComparison.Ordinal);
        Assert.Contains("Rerun", File.ReadAllText(rerun), StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_template_catalog_uses_provider_sources()
    {
        string catalog = Path.Combine(SourceRoot, "Kuroe.Kernel", "Control", "Runtime", "FlowTemplateCatalog.cs");
        string catalogText = File.ReadAllText(catalog);

        Assert.Contains("IFlowTemplateSource", catalogText, StringComparison.Ordinal);
        Assert.DoesNotContain("IFlowProviderSource", catalogText, StringComparison.Ordinal);
        Assert.DoesNotContain("FlowProvider", catalogText, StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_control_uses_one_execution_gateway()
    {
        string gateway = Path.Combine(KernelRoot, "Control", "Shared", "IFlowExecutionGateway.cs");
        string graphFactory = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Control",
            "Graph",
            "Runtime",
            "Engine",
            "GraphFlowControlFactory.cs");
        string dialogueFactory = Path.Combine(
            SourceRoot,
            "Kuroe",
            "Assistant",
            "Core",
            "Dialogue",
            "Flow",
            "DialogueFlowControlFactory.cs");
        string gatewayText = File.ReadAllText(gateway);

        Assert.Contains("RunAsync", gatewayText, StringComparison.Ordinal);
        Assert.Contains("RunRestoreAsync", gatewayText, StringComparison.Ordinal);
        Assert.Contains("PrepareRestore", gatewayText, StringComparison.Ordinal);
        Assert.DoesNotContain("Func<", gatewayText, StringComparison.Ordinal);
        Assert.DoesNotContain("IExecutionRuntime", File.ReadAllText(graphFactory), StringComparison.Ordinal);
        Assert.DoesNotContain("IExecutionRuntime", File.ReadAllText(dialogueFactory), StringComparison.Ordinal);
    }

    [Fact]
    public void Llm_execution_parameters_do_not_reference_node_type()
    {
        string file = Path.Combine(
            SourceRoot,
            "Kuroe",
            "Runner",
            "Llm",
            "Core",
            "LlmExecutionParameters.cs");

        string text = File.ReadAllText(file);
        Assert.DoesNotContain("LlmNodeType", text, StringComparison.Ordinal);
        Assert.DoesNotContain("LlmParameters", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Kernel_projects_reference_only_kernel()
    {
        IReadOnlyList<string> projects = [.. ProjectFiles(KernelRoot)];

        Assert.NotEmpty(projects);
        foreach (string project in projects)
        {
            foreach (string reference in ProjectReferences(project))
            {
                Assert.True(
                    IsUnder(reference, KernelRoot),
                    $"{Path.GetFileName(project)} 引用了 Kernel 之外的 {reference}。");
            }
        }
    }

    [Fact]
    public void Kernel_sources_stay_free_of_graph_and_node_vocabulary()
    {
        string pattern = @"(?<![A-Za-z])(Graph|Node)(?![A-Za-z])";

        Assert.NotEmpty(SourceFiles(KernelRoot));
        foreach (string file in SourceFiles(KernelRoot))
        {
            string text = File.ReadAllText(file);

            Assert.False(
                System.Text.RegularExpressions.Regex.IsMatch(text, pattern),
                $"{Path.GetFileName(file)} 出现图或节点词汇。");
        }
    }

    [Fact]
    public void Kernel_has_no_concrete_flow_records()
    {
        Assert.NotEmpty(SourceFiles(KernelRoot));
        foreach (string file in SourceFiles(KernelRoot))
        {
            string text = File.ReadAllText(file);

            Assert.DoesNotContain("CodeFlowDefinitionRecord", text, StringComparison.Ordinal);
            Assert.DoesNotContain("CodeFlowStateRecord", text, StringComparison.Ordinal);
            Assert.DoesNotContain("FlowRecordKinds", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Flow_record_kind_constants_live_with_implementations()
    {
        string graph = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Graph",
            "Records",
            "Shared",
            "GraphFlowKind.cs");
        string code = Path.Combine(
            SourceRoot,
            "Kuroe.CodeFlow",
            "Records",
            "Shared",
            "CodeFlowKind.cs");
        string taskRecord = Path.Combine(KernelRoot, "Tasks", "Records", "Shared", "TaskRecord.cs");

        Assert.True(File.Exists(graph));
        Assert.True(File.Exists(code));
        Assert.DoesNotContain("FlowRecordKinds", File.ReadAllText(taskRecord), StringComparison.Ordinal);
    }

    [Fact]
    public void Maf_control_references_only_kernel_and_maf_records()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Maf",
            "Control",
            "Core",
            "Kuroe.Maf.Control.csproj");
        string control = Path.Combine(KernelRoot, "Control", "Shared");
        string execution = Path.Combine(KernelRoot, "Execution", "Shared");
        string executionRecords = Path.Combine(KernelRoot, "Execution", "Records", "Shared");
        string presentation = Path.Combine(KernelRoot, "Presentation", "Shared");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string taskRecords = Path.Combine(KernelRoot, "Tasks", "Records", "Shared");
        string mafRecords = Path.Combine(SourceRoot, "Kuroe.Maf", "Records", "Shared");
        string mafDefinition = Path.Combine(SourceRoot, "Kuroe.Maf", "Definition", "Core");

        Assert.All(
            ProjectReferences(project),
            reference => Assert.True(
                IsUnder(reference, control)
                    || IsUnder(reference, execution)
                    || IsUnder(reference, executionRecords)
                    || IsUnder(reference, presentation)
                    || IsUnder(reference, tasks)
                    || IsUnder(reference, taskRecords)
                    || IsUnder(reference, mafRecords)
                    || IsUnder(reference, mafDefinition),
                $"Kuroe.Maf.Control 引用了契约层、MAF 记录与 MAF 定义之外的 {reference}。"));
    }

    [Fact]
    public void Maf_definition_references_only_kernel_contracts()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Maf",
            "Definition",
            "Core",
            "Kuroe.Maf.Definition.csproj");
        string execution = Path.Combine(KernelRoot, "Execution", "Shared");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string definition = Path.Combine(SourceRoot, "Kuroe.Maf", "Definition", "Core");

        Assert.All(
            ProjectReferences(project),
            reference => Assert.True(
                IsUnder(reference, execution) || IsUnder(reference, tasks),
                $"Kuroe.Maf.Definition 引用了执行共享与任务共享之外的 {reference}。"));

        foreach (string file in SourceFiles(definition))
        {
            string text = File.ReadAllText(file);

            Assert.DoesNotContain("Microsoft.Agents.AI.Workflows", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Kuroe.Control", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Kuroe.Presentation", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Maf_types_stay_out_of_kernel_and_hosts()
    {
        Assert.NotEmpty(SourceFiles(KernelRoot));
        foreach (string file in SourceFiles(KernelRoot))
        {
            string text = File.ReadAllText(file);

            Assert.DoesNotContain("Microsoft.Agents.AI.Workflows", text, StringComparison.Ordinal);
            Assert.DoesNotContain("MafFlow", text, StringComparison.Ordinal);
        }

        string[] hosts =
        [
            Path.Combine(SourceRoot, "Kuroe.Console"),
            Path.Combine(SourceRoot, "Kuroe.Cli"),
            Path.Combine(SourceRoot, "Kuroe", "Assistant", "Core"),
        ];

        foreach (string directory in hosts)
        {
            Assert.NotEmpty(SourceFiles(directory));
            foreach (string file in SourceFiles(directory))
            {
                Assert.DoesNotContain(
                    "using Kuroe.Maf.Control",
                    File.ReadAllText(file),
                    StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Maf_record_kind_lives_with_its_implementation()
    {
        string kind = Path.Combine(
            SourceRoot,
            "Kuroe.Maf",
            "Records",
            "Shared",
            "MafFlowKind.cs");
        string taskRecord = Path.Combine(KernelRoot, "Tasks", "Records", "Shared", "TaskRecord.cs");

        Assert.True(File.Exists(kind));
        Assert.DoesNotContain("MafFlowKind", File.ReadAllText(taskRecord), StringComparison.Ordinal);
    }

    [Fact]
    public void Maf_projects_stay_free_of_flow_area()
    {
        string maf = Path.Combine(SourceRoot, "Kuroe.Maf");
        string flow = Path.Combine(SourceRoot, "Kuroe.Flow");

        Assert.NotEmpty(ProjectFiles(maf));
        foreach (string project in ProjectFiles(maf))
        {
            Assert.DoesNotContain(
                ProjectReferences(project),
                reference => IsUnder(reference, flow));
        }
    }

    [Fact]
    public void Flow_area_stays_free_of_maf()
    {
        string flow = Path.Combine(SourceRoot, "Kuroe.Flow");
        string maf = Path.Combine(SourceRoot, "Kuroe.Maf");

        Assert.NotEmpty(ProjectFiles(flow));
        foreach (string project in ProjectFiles(flow))
        {
            Assert.DoesNotContain(
                ProjectReferences(project),
                reference => IsUnder(reference, maf));
        }

        foreach (string file in SourceFiles(flow))
        {
            string text = File.ReadAllText(file);

            Assert.DoesNotContain("MafFlow", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Microsoft.Agents.AI.Workflows", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Flow_area_stays_free_of_code_flow()
    {
        string flow = Path.Combine(SourceRoot, "Kuroe.Flow");
        string code = Path.Combine(SourceRoot, "Kuroe.CodeFlow");

        Assert.NotEmpty(ProjectFiles(flow));
        foreach (string project in ProjectFiles(flow))
        {
            Assert.DoesNotContain(
                ProjectReferences(project),
                reference => IsUnder(reference, code));
        }

        foreach (string file in SourceFiles(flow))
        {
            Assert.DoesNotContain("Kuroe.CodeFlow", File.ReadAllText(file), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Code_flow_area_stays_free_of_graph_and_node()
    {
        string code = Path.Combine(SourceRoot, "Kuroe.CodeFlow");

        Assert.NotEmpty(ProjectFiles(code));
        foreach (string file in SourceFiles(code))
        {
            string text = File.ReadAllText(file);

            Assert.DoesNotContain("Kuroe.Flow.Node", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Kuroe.Flow.Graph", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Kuroe.Flow.Control", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Flow_record_codec_port_lives_in_kernel()
    {
        string port = Path.Combine(KernelRoot, "Tasks", "Records", "Shared", "FlowRecordCodec.cs");
        string serialization = Path.Combine(SourceRoot, "Kuroe.Kernel", "Tasks", "Persistence", "Serialization", "Shared");

        Assert.True(File.Exists(port));
        Assert.Contains("interface IFlowRecordCodec", File.ReadAllText(port), StringComparison.Ordinal);
        Assert.DoesNotContain(
            "interface IFlowRecordCodec",
            string.Concat(SourceFiles(serialization).Select(File.ReadAllText)),
            StringComparison.Ordinal);
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

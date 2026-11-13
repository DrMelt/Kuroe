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
    public void Execution_shared_project_has_no_project_references()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Execution",
            "Shared",
            "Kuroe.Execution.Shared.csproj");

        Assert.Empty(ProjectReferences(project));
    }

    [Fact]
    public void Execution_projects_stay_free_of_flow_control_vocabulary()
    {
        string execution = Path.Combine(SourceRoot, "Kuroe.Execution");
        string pattern = @"(?<![A-Za-z])(FlowStepPath|StartInputs|FlowName|FlowSeed|PortName)(?![A-Za-z])";

        Assert.NotEmpty(SourceFiles(execution));
        foreach (string file in SourceFiles(execution))
        {
            string text = File.ReadAllText(file);

            Assert.False(
                System.Text.RegularExpressions.Regex.IsMatch(text, pattern),
                $"{Path.GetFileName(file)} 使用了流程控制词汇。");
            Assert.DoesNotContain("Kuroe.Flow.Tasks", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Kuroe.Flow.Node", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Kuroe.Flow.Control", text, StringComparison.Ordinal);
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
            Assert.DoesNotContain("using Kuroe.Flow.Tasks", text, StringComparison.Ordinal);
        }
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

        string execution = Path.Combine(SourceRoot, "Kuroe.Execution", "Shared");
        string tasks = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Shared");

        Assert.All(
            ProjectReferences(project),
            reference => Assert.True(
                IsUnder(reference, execution) || IsUnder(reference, tasks),
                $"Kuroe.Flow.Presentation.Shared 引用了中性契约之外的 {reference}。"));
    }

    [Fact]
    public void Execution_core_references_only_execution_shared()
    {
        string shared = Path.Combine(
            SourceRoot,
            "Kuroe.Execution",
            "Shared",
            "Kuroe.Execution.Shared.csproj");
        string core = Path.Combine(
            SourceRoot,
            "Kuroe.Execution",
            "Core",
            "Kuroe.Execution.csproj");
        IReadOnlyList<string> references = ProjectReferences(core);

        Assert.Equal([shared], references);
    }

    [Fact]
    public void Execution_projects_stay_free_of_graph_and_node_semantics()
    {
        string execution = Path.Combine(SourceRoot, "Kuroe.Execution");
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

        foreach (string word in (string[])["GraphFlowControl", "TaskRuntime", "GraphExecution", "NodeGraph"])
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
            reference => IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Execution", "Core")));
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
        string core = Path.Combine(SourceRoot, "Kuroe.Flow", "Core");
        string graph = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Graph");

        Assert.True(File.Exists(Path.Combine(core, "Runtime", "Tasks", "WorkTask.cs")));
        Assert.False(File.Exists(Path.Combine(graph, "Runtime", "Tasks", "WorkTask.cs")));
        Assert.True(File.Exists(Path.Combine(graph, "Runtime", "Engine", "GraphFlowControl.cs")));
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
        string execution = Path.Combine(SourceRoot, "Kuroe.Execution", "Core");
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
        string execution = Path.Combine(SourceRoot, "Kuroe.Execution", "Shared");
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
        string execution = Path.Combine(SourceRoot, "Kuroe.Execution", "Shared");

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
        string execution = Path.Combine(SourceRoot, "Kuroe.Execution", "Shared");
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
        string execution = Path.Combine(SourceRoot, "Kuroe.Execution", "Shared");
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
    public void Flow_restore_resolves_by_identity()
    {
        string framework = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Control",
            "Shared",
            "IFlowIdentity.cs");
        string contract = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Control",
            "Shared",
            "IFlowProviderSource.cs");
        string source = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Core",
            "Runtime",
            "Tasks",
            "FlowProviderSource.cs");
        string restore = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Core",
            "Runtime",
            "Tasks",
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
            "Kuroe.Flow",
            "Core",
            "Runtime",
            "Tasks",
            "TaskActions.cs");
        string text = File.ReadAllText(actions);

        Assert.DoesNotContain("Func<TaskId, ErrorOr<FlowSubmission>>", text, StringComparison.Ordinal);
        Assert.Contains("provider.Submission.Prepare", text, StringComparison.Ordinal);
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
            IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Execution")));
    }

    [Fact]
    public void Execution_records_project_references_only_execution_shared()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Execution",
            "Records",
            "Shared",
            "Kuroe.Execution.Records.Shared.csproj");

        IReadOnlyList<string> references = ProjectReferences(project);

        Assert.Single(references);
        Assert.Contains(
            Path.Combine(SourceRoot, "Kuroe.Execution", "Shared", "Kuroe.Execution.Shared.csproj"),
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
    public void Tasks_records_project_references_only_task_and_execution()
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
        string executionRecords = Path.Combine(SourceRoot, "Kuroe.Execution", "Records", "Shared");
        string execution = Path.Combine(SourceRoot, "Kuroe.Execution", "Shared");

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
        string tasks = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Shared");
        string tasksRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Records", "Shared");
        string execution = Path.Combine(SourceRoot, "Kuroe.Execution", "Shared");
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
        string executionContracts = Path.Combine(SourceRoot, "Kuroe.Execution", "Shared");
        string executionRecords = Path.Combine(SourceRoot, "Kuroe.Execution", "Records", "Shared");
        string taskRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Records", "Shared");
        string graphRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Graph", "Records", "Shared");
        string nodeContracts = Path.Combine(SourceRoot, "Kuroe.Flow", "Node", "Shared");

        foreach (string project in ProjectFiles(persistence))
        {
            IReadOnlyList<string> references = [.. ProjectReferences(project)
                .Where(reference => IsUnder(reference, SourceRoot))];
            bool graphCodec = IsUnder(project, Path.Combine(persistence, "Serialization", "Graph"));

            Assert.All(references, reference =>
                Assert.True(
                    IsPersistenceBoundary(reference, persistence)
                        || IsUnder(reference, taskContracts)
                        || IsUnder(reference, taskPresentation)
                        || IsUnder(reference, executionContracts)
                        || IsUnder(reference, executionRecords)
                        || IsUnder(reference, taskRecords)
                        || graphCodec && IsUnder(reference, flowContracts)
                        || graphCodec && IsUnder(reference, graphRecords)
                        || graphCodec && IsUnder(reference, nodeContracts),
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
        string persistence = Path.Combine(SourceRoot, "Kuroe.Flow", "Persistence");
        string[] projects =
        [
            Path.Combine(persistence, "Files", "Shared", "Kuroe.Flow.Persistence.Files.Shared.csproj"),
            Path.Combine(persistence, "Files", "Core", "Kuroe.Flow.Persistence.Files.csproj"),
            Path.Combine(persistence, "Restoring", "Shared", "Kuroe.Flow.Persistence.Restoring.Shared.csproj"),
            Path.Combine(persistence, "Restoring", "Core", "Kuroe.Flow.Persistence.Restoring.csproj"),
            Path.Combine(persistence, "Saving", "Shared", "Kuroe.Flow.Persistence.Saving.Shared.csproj"),
            Path.Combine(persistence, "Saving", "Core", "Kuroe.Flow.Persistence.Saving.csproj"),
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
        string persistence = Path.Combine(SourceRoot, "Kuroe.Flow", "Persistence");
        string[] projects =
        [
            Path.Combine(persistence, "Serialization", "Shared", "Kuroe.Flow.Persistence.Serialization.Shared.csproj"),
            Path.Combine(persistence, "Serialization", "Json", "Core", "Kuroe.Flow.Persistence.Json.csproj"),
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
    public void Control_contract_project_stays_free_of_graph_and_node()
    {
        string contract = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Contract");
        string project = Path.Combine(contract, "Kuroe.Flow.Control.Contract.csproj");
        string control = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Shared");
        string execution = Path.Combine(SourceRoot, "Kuroe.Execution", "Shared");

        Assert.All(ProjectReferences(project), reference => Assert.True(
            IsUnder(reference, control) || IsUnder(reference, execution),
            $"Kuroe.Flow.Control.Contract 引用了中性契约之外的 {reference}。"));

        foreach (string file in SourceFiles(contract))
        {
            string text = File.ReadAllText(file);
            Assert.DoesNotContain("GraphFlowControl", text, StringComparison.Ordinal);
            Assert.DoesNotContain("GraphExecution", text, StringComparison.Ordinal);
            Assert.DoesNotContain("NodeGraph", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Flow_record_codecs_live_outside_central_serialization()
    {
        string persistence = Path.Combine(SourceRoot, "Kuroe.Flow", "Persistence", "Serialization");

        Assert.True(File.Exists(Path.Combine(persistence, "Graph", "Core", "GraphFlowRecordCodec.cs")));
        Assert.True(File.Exists(Path.Combine(persistence, "Code", "Core", "CodeFlowRecordCodec.cs")));
        Assert.True(File.Exists(Path.Combine(persistence, "Shared", "FlowRecordCodec.cs")));
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

        // 执行单元的共享契约是中性基础层，任何共享层都可引用
        string executionContracts = Path.Combine(SourceRoot, "Kuroe.Execution");
        if (Directory.Exists(executionContracts))
        {
            directories.AddRange(Directory
                .EnumerateDirectories(executionContracts, "Shared", SearchOption.AllDirectories)
                .Where(directory => !IsBuildOutput(directory)));
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
        string execution = Path.Combine(SourceRoot, "Kuroe.Execution", "Shared");
        string presentation = Path.Combine(SourceRoot, "Kuroe.Flow", "Presentation", "Shared");

        Assert.All(
            ProjectReferences(project),
            reference => Assert.True(
                IsUnder(reference, tasks)
                    || IsUnder(reference, tasksPresentation)
                    || IsUnder(reference, tasksRecords)
                    || IsUnder(reference, execution)
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
        Assert.Contains("Dictionary<ExecutionId, GraphExecutionContext>", text, StringComparison.Ordinal);
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
    public void Task_snapshots_are_assembled_by_the_task_domain()
    {
        string control = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Shared");
        string factory = Path.Combine(control, "TaskSnapshotFactory.cs");
        string graph = Path.Combine(
            SourceRoot, "Kuroe.Flow", "Control", "Graph", "Runtime", "Engine", "GraphFlowPresentation.cs");
        string dialogue = Path.Combine(
            SourceRoot, "Kuroe", "Assistant", "Core", "Dialogue", "DialogueFlowPresentation.cs");
        string workTask = Path.Combine(
            SourceRoot, "Kuroe.Flow", "Core", "Runtime", "Tasks", "WorkTask.cs");

        Assert.True(File.Exists(factory));
        Assert.Contains("TaskSnapshotFactory", File.ReadAllText(workTask), StringComparison.Ordinal);
        Assert.DoesNotContain("new TaskSnapshot", File.ReadAllText(graph), StringComparison.Ordinal);
        Assert.DoesNotContain("new TaskSnapshot", File.ReadAllText(dialogue), StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_ports_expose_views_pending_steps_and_flow_records()
    {
        string presentation = Path.Combine(
            SourceRoot, "Kuroe.Flow", "Control", "Shared", "IFlowPresentation.cs");
        string records = Path.Combine(
            SourceRoot, "Kuroe.Flow", "Control", "Shared", "IFlowRecordSource.cs");
        string graphRecords = Path.Combine(
            SourceRoot, "Kuroe.Flow", "Control", "Graph", "Runtime", "Engine", "GraphFlowRecordSource.cs");
        string dialogueRecords = Path.Combine(
            SourceRoot, "Kuroe", "Assistant", "Core", "Dialogue", "DialogueFlowRecordSource.cs");

        Assert.DoesNotContain("CreateSnapshot", File.ReadAllText(presentation), StringComparison.Ordinal);
        Assert.Contains("Pending", File.ReadAllText(presentation), StringComparison.Ordinal);
        Assert.DoesNotContain("CaptureExecutions", File.ReadAllText(records), StringComparison.Ordinal);
        Assert.DoesNotContain("CaptureExecutions", File.ReadAllText(graphRecords), StringComparison.Ordinal);
        Assert.DoesNotContain("CaptureExecutions", File.ReadAllText(dialogueRecords), StringComparison.Ordinal);
    }

    [Fact]
    public void Execution_records_capture_the_source_key()
    {
        string capture = Path.Combine(
            SourceRoot, "Kuroe.Execution", "Records", "Shared", "ExecutionRecordCapture.cs");

        Assert.Contains("SourceKey", File.ReadAllText(capture), StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_name_lives_in_neutral_tasks()
    {
        string file = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Shared", "FlowName.cs");

        Assert.Contains("namespace Kuroe.Flow.Tasks.Shared;", File.ReadAllText(file), StringComparison.Ordinal);
    }

    [Fact]
    public void Execution_records_stay_free_of_graph_and_node_types()
    {
        string records = Path.Combine(SourceRoot, "Kuroe.Execution", "Records", "Shared");
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
            "Kuroe.Flow",
            "Runtime",
            "Core",
            "Kuroe.Flow.Runtime.csproj");
        string control = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Shared");
        string execution = Path.Combine(SourceRoot, "Kuroe.Execution", "Shared");
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
        string execution = Path.Combine(SourceRoot, "Kuroe.Execution", "Shared");
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
            Path.Combine(SourceRoot, "Kuroe", "Assistant", "Core", "Flows"),
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
        string control = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Shared");
        string shared = Path.Combine(SourceRoot, "Kuroe.Flow", "Shared");
        string execution = Path.Combine(SourceRoot, "Kuroe.Execution", "Shared");
        string executionRecords = Path.Combine(SourceRoot, "Kuroe.Execution", "Records", "Shared");
        string graphRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Graph", "Records", "Shared");
        string inputChannel = Path.Combine(SourceRoot, "Kuroe.Flow", "InputChannel", "Shared");
        string nodes = Path.Combine(SourceRoot, "Kuroe.Flow", "Node", "Shared");
        string presentation = Path.Combine(SourceRoot, "Kuroe.Flow", "Presentation", "Shared");
        string tasks = Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks", "Shared");
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
                    || IsUnder(reference, taskRecords),
                $"Kuroe.Flow.Control.Graph 引用了契约层与图记录之外的 {reference}。"));
    }

    [Fact]
    public void Execution_shared_defines_no_concrete_state_machine()
    {
        string shared = Path.Combine(SourceRoot, "Kuroe.Execution", "Shared");
        string core = Path.Combine(SourceRoot, "Kuroe.Execution", "Core");

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
            Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Shared"),
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
            "Kuroe.Execution",
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

    [Fact]
    public void Flow_control_port_excludes_input_delivery()
    {
        string control = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Shared", "IFlowControl.cs");
        string input = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Shared", "IFlowInputControl.cs");
        string controlText = File.ReadAllText(control);

        Assert.DoesNotContain("Pending", controlText, StringComparison.Ordinal);
        Assert.DoesNotContain("Deliver", controlText, StringComparison.Ordinal);
        Assert.Contains("Pending", File.ReadAllText(input), StringComparison.Ordinal);
        Assert.Contains("Deliver", File.ReadAllText(input), StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_template_catalog_uses_provider_sources()
    {
        string catalog = Path.Combine(SourceRoot, "Kuroe", "Core", "Workflows", "Flows", "FlowTemplateCatalog.cs");
        string provider = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Shared", "FlowProvider.cs");
        string catalogText = File.ReadAllText(catalog);

        Assert.Contains("IFlowProviderSource", catalogText, StringComparison.Ordinal);
        Assert.DoesNotContain("IFlowCatalog", catalogText, StringComparison.Ordinal);
        Assert.Contains("IFlowTemplateSource Templates", File.ReadAllText(provider), StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_control_uses_one_execution_gateway()
    {
        string gateway = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Shared", "IFlowExecutionGateway.cs");
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
            "Node",
            "Llm",
            "Core",
            "LlmExecutionParameters.cs");

        Assert.DoesNotContain("LlmNodeType", File.ReadAllText(file), StringComparison.Ordinal);
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

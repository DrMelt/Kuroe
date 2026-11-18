using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryAssertions;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>流程区域边界：共享层是纯模型，节点与图各自持有实现，流程区域与代码流程互不引用。</summary>
public sealed class FlowAreaTests
{
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

        ReferencesStayUnder(project, "Kuroe.Flow.Node.Shared", "契约层", tasks, execution, shared);
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

        ReferencesStayUnder(project, "Kuroe.Flow.Shared", "中性共享", tasks, execution);
    }

    [Fact]
    public void Flow_area_stays_free_of_task_and_presentation_projects()
    {
        Assert.False(Directory.Exists(Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks")));
        Assert.False(Directory.Exists(Path.Combine(SourceRoot, "Kuroe.Flow", "Presentation")));
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
            SourcesExclude(directory, "重复定义的执行单元", forbidden);
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

        ReferencesStayUnder(
            project,
            "Kuroe.Flow.Control.Graph",
            "契约层与图记录",
            control,
            shared,
            execution,
            executionRecords,
            graphRecords,
            nodes,
            presentation,
            tasks,
            taskRecords);
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

        SourcesExclude(directory, "执行状态机写入口", forbidden);
    }

    [Fact]
    public void Graph_control_does_not_reference_input()
    {
        string project = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Graph", "Kuroe.Flow.Control.Graph.csproj");
        string input = Path.Combine(KernelRoot, "Input");

        Assert.DoesNotContain(ProjectReferences(project), reference => IsUnder(reference, input));
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
}

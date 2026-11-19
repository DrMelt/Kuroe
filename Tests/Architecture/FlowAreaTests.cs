using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryAssertions;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>流程区域边界：共享层是纯模型，节点与图各自持有实现，流程区域与代码流程互不引用。</summary>
public sealed class FlowAreaTests
{
    [Fact(DisplayName = "流程宿主不引用执行核")]
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

    [Fact(DisplayName = "节点项目不引用流程实现")]
    public void Node_projects_do_not_reference_flow_implementation()
    {
        string nodes = Path.Combine(SourceRoot, "Kuroe", "Node");
        string execution = Path.Combine(KernelRoot, "Execution", "Core");
        string flow = Path.Combine(SourceRoot, "Kuroe.Kernel", "Tasks", "Runtime");
        string graphRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Records");

        Assert.NotEmpty(ProjectFiles(nodes));
        foreach (string project in ProjectFiles(nodes))
        {
            IReadOnlyList<string> references = ProjectReferences(project);

            Assert.DoesNotContain(references, reference => IsUnder(reference, execution));
            Assert.DoesNotContain(references, reference => IsUnder(reference, flow));
            Assert.DoesNotContain(references, reference => IsUnder(reference, graphRecords));
        }
    }

    [Fact(DisplayName = "节点项目引用图共享层")]
    public void Node_projects_reference_graph_shared()
    {
        string nodes = Path.Combine(SourceRoot, "Kuroe", "Node");
        string graphShared = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Graph",
            "Shared",
            "Kuroe.Flow.Graph.Shared.csproj");

        Assert.NotEmpty(ProjectFiles(nodes));
        foreach (string project in ProjectFiles(nodes))
        {
            Assert.Contains(graphShared, ProjectReferences(project));
        }
    }

    [Fact(DisplayName = "图共享层只引用契约层与执行器共享层")]
    public void Graph_shared_project_references_only_contract_layers()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Graph",
            "Shared",
            "Kuroe.Flow.Graph.Shared.csproj");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string execution = Path.Combine(KernelRoot, "Execution", "Shared");
        string executor = Path.Combine(SourceRoot, "Kuroe.Executor", "Shared");

        ReferencesStayUnder(project, "Kuroe.Flow.Graph.Shared", "契约层与执行器共享层", tasks, execution, executor);
    }

    [Fact(DisplayName = "流程区域不含任务与表现项目")]
    public void Flow_area_stays_free_of_task_and_presentation_projects()
    {
        Assert.False(Directory.Exists(Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks")), $"{Path.Combine(SourceRoot, "Kuroe.Flow", "Tasks")} 不应存在。");
        Assert.False(Directory.Exists(Path.Combine(SourceRoot, "Kuroe.Flow", "Presentation")), $"{Path.Combine(SourceRoot, "Kuroe.Flow", "Presentation")} 不应存在。");
    }

    [Fact(DisplayName = "图状态保存执行上下文而不是对象")]
    public void Graph_state_stores_execution_contexts_not_objects()
    {
        string file = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Control",
            "Core",
            "Runtime",
            "Graph",
            "GraphFlowState.cs");
        string text = File.ReadAllText(file);

        Assert.DoesNotContain("private readonly List<GraphExecution>", text, StringComparison.Ordinal);
        Assert.DoesNotContain("public List<GraphExecution>", text, StringComparison.Ordinal);
        Assert.Contains("Dictionary<ExecutionId, GraphExecutionContext>", text, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "流程实现按端口类别拆分文件")]
    public void Flow_implementations_split_ports_by_class()
    {
        string graph = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Core", "Runtime", "Engine");
        string dialogue = Path.Combine(SourceRoot, "Kuroe", "Assistant", "Core", "Dialogue", "Flow");

        Assert.False(File.Exists(Path.Combine(graph, "GraphFlowFramework.cs")), $"{Path.Combine(graph, "GraphFlowFramework.cs")} 不应存在。");
        Assert.False(File.Exists(Path.Combine(dialogue, "DialogueFlowFramework.cs")), $"{Path.Combine(dialogue, "DialogueFlowFramework.cs")} 不应存在。");
        Assert.True(File.Exists(Path.Combine(graph, "GraphFlowIdentity.cs")), $"{Path.Combine(graph, "GraphFlowIdentity.cs")} 不存在。");
        Assert.True(File.Exists(Path.Combine(graph, "GraphFlowSubmission.cs")), $"{Path.Combine(graph, "GraphFlowSubmission.cs")} 不存在。");
        Assert.True(File.Exists(Path.Combine(graph, "GraphFlowStateBinder.cs")), $"{Path.Combine(graph, "GraphFlowStateBinder.cs")} 不存在。");
        Assert.True(File.Exists(Path.Combine(graph, "GraphFlowControlFactory.cs")), $"{Path.Combine(graph, "GraphFlowControlFactory.cs")} 不存在。");
        Assert.True(File.Exists(Path.Combine(graph, "GraphFlowPresentation.cs")), $"{Path.Combine(graph, "GraphFlowPresentation.cs")} 不存在。");
        Assert.True(File.Exists(Path.Combine(graph, "GraphFlowRecordSource.cs")), $"{Path.Combine(graph, "GraphFlowRecordSource.cs")} 不存在。");
        Assert.True(File.Exists(Path.Combine(dialogue, "DialogueFlowIdentity.cs")), $"{Path.Combine(dialogue, "DialogueFlowIdentity.cs")} 不存在。");
        Assert.True(File.Exists(Path.Combine(dialogue, "DialogueFlowSubmission.cs")), $"{Path.Combine(dialogue, "DialogueFlowSubmission.cs")} 不存在。");
        Assert.True(File.Exists(Path.Combine(dialogue, "DialogueFlowControlFactory.cs")), $"{Path.Combine(dialogue, "DialogueFlowControlFactory.cs")} 不存在。");
        Assert.True(File.Exists(Path.Combine(dialogue, "DialogueFlowPresentation.cs")), $"{Path.Combine(dialogue, "DialogueFlowPresentation.cs")} 不存在。");
        Assert.True(File.Exists(Path.Combine(dialogue, "DialogueFlowRecordSource.cs")), $"{Path.Combine(dialogue, "DialogueFlowRecordSource.cs")} 不存在。");
    }

    [Fact(DisplayName = "图执行接口命名为 IGraphExecution")]
    public void Graph_execution_interface_is_named_IGraphExecution()
    {
        string execution = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Graph",
            "Shared",
            "RuntimeGraph",
            "Execution");

        Assert.True(File.Exists(Path.Combine(execution, "IGraphExecution.cs")), $"{Path.Combine(execution, "IGraphExecution.cs")} 不存在。");
        Assert.False(File.Exists(Path.Combine(execution, "IExecution.cs")), $"{Path.Combine(execution, "IExecution.cs")} 不应存在。");
        Assert.Contains(
            "interface IGraphExecution",
            File.ReadAllText(Path.Combine(execution, "IGraphExecution.cs")),
            StringComparison.Ordinal);
    }

    [Fact(DisplayName = "图源码不重复定义执行身份与状态")]
    public void Graph_sources_do_not_redefine_execution_identity_or_state()
    {
        string execution = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Graph",
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
            Path.Combine(SourceRoot, "Kuroe.Flow", "Graph"),
            Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Core"),
        ];

        foreach (string directory in directories)
        {
            SourcesExclude(directory, "重复定义的执行单元", forbidden);
        }
    }

    [Fact(DisplayName = "控制图只引用契约与图层")]
    public void Control_graph_references_only_contract_and_graph_layers()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Control",
            "Core",
            "Kuroe.Flow.Control.csproj");
        string control = Path.Combine(KernelRoot, "Control", "Shared");
        string execution = Path.Combine(KernelRoot, "Execution", "Shared");
        string executionRecords = Path.Combine(KernelRoot, "Execution", "Records", "Shared");
        string graphRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Records", "Shared");
        string nodes = Path.Combine(SourceRoot, "Kuroe.Flow", "Graph", "Shared");
        string presentation = Path.Combine(KernelRoot, "Presentation", "Shared");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string taskRecords = Path.Combine(KernelRoot, "Tasks", "Records", "Shared");
        string executor = Path.Combine(SourceRoot, "Kuroe.Executor", "Shared");

        ReferencesStayUnder(
            project,
            "Kuroe.Flow.Control",
            "契约层、执行器共享层与图记录",
            control,
            execution,
            executionRecords,
            graphRecords,
            nodes,
            presentation,
            tasks,
            taskRecords,
            executor);
    }

    [Fact(DisplayName = "图控制不驱动执行状态机")]
    public void Graph_control_does_not_drive_the_execution_state_machine()
    {
        string directory = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Core");
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

    [Fact(DisplayName = "图控制不引用输入模块")]
    public void Graph_control_does_not_reference_input()
    {
        string project = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Core", "Kuroe.Flow.Control.csproj");
        string input = Path.Combine(KernelRoot, "Input");

        Assert.DoesNotContain(ProjectReferences(project), reference => IsUnder(reference, input));
    }

    [Fact(DisplayName = "流程区域不含代码流程")]
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

    [Fact(DisplayName = "代码流程区域不含图与节点实现")]
    public void Code_flow_area_stays_free_of_graph_and_node()
    {
        string code = Path.Combine(SourceRoot, "Kuroe.CodeFlow");

        Assert.NotEmpty(ProjectFiles(code));
        foreach (string file in SourceFiles(code))
        {
            string text = File.ReadAllText(file);

            Assert.DoesNotContain("Kuroe.Flow.Graph", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Kuroe.Flow.Records", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Kuroe.Flow.Control", text, StringComparison.Ordinal);
        }
    }
}

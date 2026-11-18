using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryAssertions;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>任务模块边界：任务运行期不引执行核与图控制，快照与提交流程由任务域装配。</summary>
public sealed class KernelTaskTests
{
    [Fact]
    public void Task_runtime_project_does_not_reference_execution_core_or_graph_control()
    {
        string project = Path.Combine(SourceRoot, "Kuroe.Kernel", "Tasks", "Runtime", "Kuroe.Tasks.Runtime.csproj");

        ReferencesExclude(
            project,
            Path.GetFileName(project),
            "执行核与图控制",
            Path.Combine(KernelRoot, "Execution", "Core"),
            Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Graph"));
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

        SourcesExclude(core, "图运行期语义", forbidden);
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
    public void Task_records_project_references_only_task_and_execution()
    {
        string project = Path.Combine(
            KernelRoot,
            "Tasks",
            "Records",
            "Shared",
            "Kuroe.Tasks.Records.Shared.csproj");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string executionRecords = Path.Combine(KernelRoot, "Execution", "Records", "Shared");
        string execution = Path.Combine(KernelRoot, "Execution", "Shared");

        ReferencesStayUnder(
            project,
            Path.GetFileName(project),
            "任务记录边界",
            tasks,
            executionRecords,
            execution);
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
    public void Flow_name_lives_in_neutral_tasks()
    {
        string file = Path.Combine(KernelRoot, "Tasks", "Shared", "FlowName.cs");

        Assert.Contains("namespace Kuroe.Tasks.Shared;", File.ReadAllText(file), StringComparison.Ordinal);
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
}

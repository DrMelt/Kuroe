using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryAssertions;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>任务模块边界：任务运行期不引执行核与图控制，快照与提交流程由任务域装配。</summary>
public sealed class KernelTaskTests
{
    [Fact(DisplayName = "任务运行期不引用执行核与图控制")]
    public void Task_runtime_project_does_not_reference_execution_core_or_graph_control()
    {
        string project = Path.Combine(SourceRoot, "Kuroe.Kernel", "Tasks", "Runtime", "Kuroe.Kernel.Tasks.Runtime.csproj");

        ReferencesExclude(
            project,
            Path.GetFileName(project),
            "执行核与图控制",
            Path.Combine(KernelRoot, "Execution", "Core"),
            Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Core"));
    }

    [Fact(DisplayName = "图运行期位于控制图层")]
    public void Graph_runtime_lives_in_control_graph()
    {
        string core = Path.Combine(SourceRoot, "Kuroe.Kernel", "Tasks", "Runtime");
        string graph = Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Core");

        Assert.True(File.Exists(Path.Combine(core, "WorkTask.cs")), $"{Path.Combine(core, "WorkTask.cs")} 不存在。");
        Assert.False(File.Exists(Path.Combine(graph, "Runtime", "Tasks", "WorkTask.cs")), $"{Path.Combine(graph, "Runtime", "Tasks", "WorkTask.cs")} 不应存在。");
        Assert.True(File.Exists(Path.Combine(graph, "Runtime", "Engine", "GraphFlowControl.cs")), $"{Path.Combine(graph, "Runtime", "Engine", "GraphFlowControl.cs")} 不存在。");
        Assert.True(File.Exists(Path.Combine(graph, "Runtime", "Graph", "TaskRuntime.cs")), $"{Path.Combine(graph, "Runtime", "Graph", "TaskRuntime.cs")} 不存在。");
    }

    [Fact(DisplayName = "任务提交通过提供方准备")]
    public void Task_submission_prepares_through_provider()
    {
        string actions = Path.Combine(
            SourceRoot,
            "Kuroe.Kernel",
            "Tasks",
            "Runtime",
            "TaskActions.cs");
        string text = File.ReadAllText(actions);

        Assert.Contains("provider.Submission.Prepare", text, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "任务共享层不含图引用")]
    public void Task_shared_project_stays_free_of_graph_references()
    {
        string project = Path.Combine(
            KernelRoot,
            "Tasks",
            "Shared",
            "Kuroe.Kernel.Tasks.Shared.csproj");
        IReadOnlyList<string> references = ProjectReferences(project);

        Assert.DoesNotContain(references, reference =>
            IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow")));
        Assert.DoesNotContain(references, reference =>
            IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Kernel", "Tasks", "Runtime")));
        Assert.DoesNotContain(references, reference =>
            IsUnder(reference, Path.Combine(KernelRoot, "Execution")));
    }

    [Fact(DisplayName = "任务记录项目只引用任务与执行")]
    public void Task_records_project_references_only_task_and_execution()
    {
        string project = Path.Combine(
            KernelRoot,
            "Tasks",
            "Records",
            "Shared",
            "Kuroe.Kernel.Tasks.Records.Shared.csproj");
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

    [Fact(DisplayName = "任务快照由任务域装配")]
    public void Task_snapshots_are_assembled_by_the_task_domain()
    {
        string control = Path.Combine(KernelRoot, "Control", "Shared");
        string factory = Path.Combine(control, "TaskSnapshotFactory.cs");
        string workTask = Path.Combine(
            SourceRoot, "Kuroe.Kernel", "Tasks", "Runtime", "WorkTask.cs");

        Assert.True(File.Exists(factory), $"{factory} 不存在。");
        Assert.Contains("TaskSnapshotFactory", File.ReadAllText(workTask), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "流程名位于中性任务层")]
    public void Flow_name_lives_in_neutral_tasks()
    {
        string file = Path.Combine(KernelRoot, "Tasks", "Shared", "FlowName.cs");

        Assert.Contains("namespace Kuroe.Kernel.Tasks.Shared;", File.ReadAllText(file), StringComparison.Ordinal);
    }
}

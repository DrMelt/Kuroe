using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryAssertions;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>表现层契约边界：表现快照只承载中性数据，不出现图或节点类型。</summary>
public sealed class PresentationBoundaryTests
{
    [Fact(DisplayName = "表现共享层不含图与控制引用")]
    public void Presentation_shared_project_stays_free_of_graph_and_control_references()
    {
        string project = Path.Combine(
            KernelRoot,
            "Presentation",
            "Shared",
            "Kuroe.Presentation.Shared.csproj");

        string execution = Path.Combine(KernelRoot, "Execution", "Shared");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");

        ReferencesStayUnder(project, "Kuroe.Presentation.Shared", "中性契约", execution, tasks);
    }

    [Fact(DisplayName = "任务表现层不含图与节点类型")]
    public void Task_presentation_stays_free_of_graph_and_node_types()
    {
        string presentation = Path.Combine(KernelRoot, "Tasks", "Presentation", "Shared");
        string[] forbidden =
        [
            "GraphFlowControl",
            "TaskRuntime",
            "NodeGraph",
            "Control.Graph",
            "NodePath",
            "ExecutableSnapshot",
            "ExecutableStateSnapshot",
            "ContainerSnapshot",
            "FlowExecutableView",
        ];

        SourcesExclude(presentation, "图或节点语义", forbidden);

        string snapshot = File.ReadAllText(Path.Combine(presentation, "TaskSnapshot.cs"));
        Assert.DoesNotContain("Executables", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutableStates", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("Containers", snapshot, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "任务表现层只引用中性契约")]
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

        ReferencesStayUnder(
            project,
            "Kuroe.Tasks.Presentation.Shared",
            "中性契约",
            tasks,
            execution,
            presentation);
    }
}

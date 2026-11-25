using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>节点与运行器边界：节点项目引用各自运行器实现，运行器与图共享层不反向依赖。</summary>
public sealed class NodeRunnerTests
{
    [Fact(DisplayName = "节点项目引用各自运行器实现")]
    public void Node_projects_reference_their_runner_implementation()
    {
        string runnerShared = Path.Combine(
            AbilityRoot, "Runner", "Shared", "Kuroe.Host.Ability.Runner.Shared.csproj");
        string runnerRoot = Path.Combine(AbilityRoot, "Runner");
        (string Project, string Runner, bool AllowsRunnerShared)[] projects =
        [
            (
                Path.Combine(AbilityRoot, "Node", "Command", "Core", "Kuroe.Host.Ability.Node.Command.csproj"),
                Path.Combine(runnerRoot, "Command", "Core", "Kuroe.Host.Ability.Runner.Command.csproj"),
                false),
            (
                Path.Combine(AbilityRoot, "Node", "File", "Core", "Kuroe.Host.Ability.Node.File.csproj"),
                Path.Combine(runnerRoot, "File", "Core", "Kuroe.Host.Ability.Runner.File.csproj"),
                false),
            (
                Path.Combine(AbilityRoot, "Node", "Llm", "Core", "Kuroe.Host.Ability.Node.Llm.csproj"),
                Path.Combine(runnerRoot, "Llm", "Core", "Kuroe.Host.Ability.Runner.Llm.csproj"),
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

    [Fact(DisplayName = "图流程节点执行装配引用输入与审批运行器")]
    public void Graph_flow_execution_assembly_references_input_and_approval_runners()
    {
        string runnerRoot = Path.Combine(AbilityRoot, "Runner");
        string graphFlow = Path.Combine(SourceRoot, "Kuroe.Host", "FlowIntegration", "GraphFlow", "Core", "Kuroe.Host.FlowIntegration.GraphFlow.csproj");

        IReadOnlyList<string> flowReferences = ProjectReferences(graphFlow);
        foreach (string runner in new[] { "Input", "Approval" })
        {
            Assert.Contains(Path.Combine(runnerRoot, runner, "Core", $"Kuroe.Host.Ability.Runner.{runner}.csproj"), flowReferences);
        }
    }

    [Fact(DisplayName = "共享节点项目不引用流程实现")]
    public void Shared_node_projects_stay_out_of_flow_implementations()
    {
        string nodes = Path.Combine(AbilityRoot, "Node");
        string flow = Path.Combine(SourceRoot, "Kuroe.Flow");
        string maf = Path.Combine(SourceRoot, "Kuroe.Maf");

        Assert.NotEmpty(ProjectFiles(nodes));
        foreach (string project in ProjectFiles(nodes))
        {
            IReadOnlyList<string> references = ProjectReferences(project);

            Assert.DoesNotContain(references, reference => IsUnder(reference, flow));
            Assert.DoesNotContain(references, reference => IsUnder(reference, maf));
        }
    }

    [Fact(DisplayName = "运行器项目不含节点与流程实现")]
    public void Runner_projects_stay_free_of_node_and_flow()
    {
        string runner = Path.Combine(AbilityRoot, "Runner");
        string[] forbidden =
        [
            Path.Combine(AbilityRoot, "Node"),
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

    [Fact(DisplayName = "进程项目是中性能力")]
    public void Process_project_is_a_neutral_capability()
    {
        Assert.False(Directory.Exists(Path.Combine(SourceRoot, "Kuroe.Host", "Process")), $"{Path.Combine(SourceRoot, "Kuroe.Host", "Process")} 不应存在。");

        string project = Path.Combine(SourceRoot, "Kuroe.Process", "Core", "Kuroe.Process.csproj");
        Assert.True(File.Exists(project), $"{project} 不存在。");
        Assert.Empty(ProjectReferences(project));
    }

    [Fact(DisplayName = "运行器共享层只引用中性契约")]
    public void Runner_shared_references_only_neutral_contracts()
    {
        string project = Path.Combine(AbilityRoot, "Runner", "Shared", "Kuroe.Host.Ability.Runner.Shared.csproj");

        Assert.Empty(ProjectReferences(project));
    }
}

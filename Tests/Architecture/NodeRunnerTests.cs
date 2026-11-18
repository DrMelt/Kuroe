using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>节点与运行器边界：节点项目引用各自运行器实现，运行器与节点共享层不反向依赖。</summary>
public sealed class NodeRunnerTests
{
    [Fact(DisplayName = "节点项目引用各自运行器实现")]
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

    [Fact(DisplayName = "运行器项目不含节点与流程实现")]
    public void Runner_projects_stay_free_of_node_and_flow()
    {
        Assert.False(Directory.Exists(Path.Combine(SourceRoot, "Kuroe.Runner")), $"{Path.Combine(SourceRoot, "Kuroe.Runner")} 不应存在。");
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

    [Fact(DisplayName = "进程项目是中性能力")]
    public void Process_project_is_a_neutral_capability()
    {
        Assert.False(Directory.Exists(Path.Combine(SourceRoot, "Kuroe", "Process")), $"{Path.Combine(SourceRoot, "Kuroe", "Process")} 不应存在。");

        string project = Path.Combine(SourceRoot, "Kuroe.Process", "Core", "Kuroe.Process.csproj");
        Assert.True(File.Exists(project), $"{project} 不存在。");
        Assert.Empty(ProjectReferences(project));
    }

    [Fact(DisplayName = "运行器共享层只引用中性契约")]
    public void Runner_shared_references_only_neutral_contracts()
    {
        string project = Path.Combine(SourceRoot, "Kuroe", "Runner", "Shared", "Kuroe.Runner.Shared.csproj");

        Assert.Empty(ProjectReferences(project));
    }

    [Fact(DisplayName = "LLM 执行参数不引用节点类型")]
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
}

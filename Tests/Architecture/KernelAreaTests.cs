using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryAssertions;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>Kernel 区域整体约束：工程只引 Kernel、源码无图节点词汇、无具体流程记录，中性共享层不使用图命名空间，Input 模块只依赖 Kernel 契约。</summary>
public sealed class KernelAreaTests
{
    [Fact(DisplayName = "Kernel 项目只引用 Kernel")]
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

    [Fact(DisplayName = "Kernel 源码不含图与节点词汇")]
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

    [Fact(DisplayName = "Kernel 不含具体流程记录")]
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

    [Fact(DisplayName = "输入项目只引用 Kernel 契约")]
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

    [Fact(DisplayName = "输入项目不含流程实现")]
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

    [Fact(DisplayName = "输入项目不含图与节点词汇")]
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

    [Fact(DisplayName = "中性共享层不使用图命名空间")]
    public void Neutral_shared_layers_use_no_graph_namespaces()
    {
        string[] directories =
        [
            Path.Combine(KernelRoot, "Control", "Shared"),
            Path.Combine(KernelRoot, "Tasks", "Presentation", "Shared"),
        ];
        string[] forbidden =
        [
            "Kuroe.Flow.Node.RuntimeGraph",
            "Kuroe.Flow.Node.StaticGraph",
            "Kuroe.Flow.Control",
            "Kuroe.Flow.Node.Shared",
        ];

        foreach (string directory in directories)
        {
            SourcesExclude(directory, "图命名空间", forbidden);
        }
    }
}

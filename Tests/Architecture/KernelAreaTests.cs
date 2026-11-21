using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>Kernel 区域整体约束：工程只引 Kernel 与文件访问契约，Input 模块只依赖 Kernel 契约。</summary>
public sealed class KernelAreaTests
{
    [Fact(DisplayName = "Kernel 项目只引用 Kernel 与文件访问契约")]
    public void Kernel_projects_reference_only_kernel_and_file_contracts()
    {
        IReadOnlyList<string> projects = [.. ProjectFiles(KernelRoot)];
        string filesShared = Path.Combine(SourceRoot, "Kuroe.Files", "Shared");

        Assert.NotEmpty(projects);
        foreach (string project in projects)
        {
            foreach (string reference in ProjectReferences(project))
            {
                Assert.True(
                    IsUnder(reference, KernelRoot) || IsUnder(reference, filesShared),
                    $"{Path.GetFileName(project)} 引用了 Kernel 之外的 {reference}。");
            }
        }
    }

    [Fact(DisplayName = "输入项目只引用 Kernel 契约")]
    public void Input_projects_reference_only_kernel_contracts()
    {
        string input = Path.Combine(KernelRoot, "Input");
        string shared = Path.Combine(input, "Shared", "Kuroe.Kernel.Input.Shared.csproj");
        string runtime = Path.Combine(input, "Runtime", "Kuroe.Kernel.Input.Runtime.csproj");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string sharedProject = Path.Combine(input, "Shared");

        Assert.Equal([Path.Combine(tasks, "Kuroe.Kernel.Tasks.Shared.csproj")], ProjectReferences(shared));
        Assert.Equal(
            [Path.Combine(sharedProject, "Kuroe.Kernel.Input.Shared.csproj"), Path.Combine(tasks, "Kuroe.Kernel.Tasks.Shared.csproj")],
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
}

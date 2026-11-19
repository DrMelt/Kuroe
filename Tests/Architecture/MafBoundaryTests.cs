using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryAssertions;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>MAF 边界：运行期层只依赖契约层与自己的记录定义，文档装配层只引用流程共享契约与流程描述。</summary>
public sealed class MafBoundaryTests
{
    [Fact(DisplayName = "Maf 控制层只引用 Kernel 与 Maf 记录")]
    public void Maf_control_references_only_kernel_and_maf_records()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Maf",
            "Control",
            "Core",
            "Kuroe.Maf.Control.csproj");
        string control = Path.Combine(KernelRoot, "Control", "Shared");
        string execution = Path.Combine(KernelRoot, "Execution", "Shared");
        string executionRecords = Path.Combine(KernelRoot, "Execution", "Records", "Shared");
        string presentation = Path.Combine(KernelRoot, "Presentation", "Shared");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string taskRecords = Path.Combine(KernelRoot, "Tasks", "Records", "Shared");
        string mafRecords = Path.Combine(SourceRoot, "Kuroe.Maf", "Records", "Shared");
        string mafDefinition = Path.Combine(SourceRoot, "Kuroe.Maf", "Definition", "Core");

        ReferencesStayUnder(
            project,
            "Kuroe.Maf.Control",
            "契约层、MAF 记录与 MAF 定义",
            control,
            execution,
            executionRecords,
            presentation,
            tasks,
            taskRecords,
            mafRecords,
            mafDefinition);
    }

    [Fact(DisplayName = "Maf 定义层只引用 Kernel 契约")]
    public void Maf_definition_references_only_kernel_contracts()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Maf",
            "Definition",
            "Core",
            "Kuroe.Maf.Definition.csproj");
        string execution = Path.Combine(KernelRoot, "Execution", "Shared");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");

        ReferencesStayUnder(project, "Kuroe.Maf.Definition", "执行共享与任务共享", execution, tasks);
    }

    [Fact(DisplayName = "Maf 运行期项目不含流程区域")]
    public void Maf_runtime_projects_stay_free_of_flow_area()
    {
        string[] runtime =
        [
            Path.Combine(SourceRoot, "Kuroe.Maf", "Core"),
            Path.Combine(SourceRoot, "Kuroe.Maf", "Control", "Core"),
            Path.Combine(SourceRoot, "Kuroe.Maf", "Definition", "Core"),
            Path.Combine(SourceRoot, "Kuroe.Maf", "Persistence", "Core"),
            Path.Combine(SourceRoot, "Kuroe.Maf", "Records", "Shared"),
        ];
        string flow = Path.Combine(SourceRoot, "Kuroe.Flow");

        foreach (string directory in runtime)
        {
            IReadOnlyList<string> projects = [.. ProjectFiles(directory)];

            Assert.NotEmpty(projects);
            foreach (string project in projects)
            {
                Assert.DoesNotContain(
                    ProjectReferences(project),
                    reference => IsUnder(reference, flow));
            }
        }
    }

    [Fact(DisplayName = "Maf 文档装配只引用流程共享契约")]
    public void Maf_compilation_references_only_flow_shared_contracts()
    {
        string document = Path.Combine(SourceRoot, "Kuroe.FlowDescription", "Shared");
        string flowGraphShared = Path.Combine(SourceRoot, "Kuroe.Flow", "Graph", "Shared");
        string flowCompilationShared = Path.Combine(SourceRoot, "Kuroe.Flow", "Compilation", "Shared");
        string mafCompilationShared = Path.Combine(SourceRoot, "Kuroe.Maf", "Compilation", "Shared");
        string mafDefinition = Path.Combine(SourceRoot, "Kuroe.Maf", "Definition", "Core");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string execution = Path.Combine(KernelRoot, "Execution", "Shared");
        string executor = Path.Combine(SourceRoot, "Kuroe.Executor", "Shared");

        string shared = Path.Combine(
            SourceRoot,
            "Kuroe.Maf",
            "Compilation",
            "Shared",
            "Kuroe.Maf.Compilation.Shared.csproj");
        string compilation = Path.Combine(
            SourceRoot,
            "Kuroe.Maf",
            "Compilation",
            "Core",
            "Kuroe.Maf.Compilation.csproj");

        ReferencesStayUnder(
            shared,
            "Kuroe.Maf.Compilation.Shared",
            "流程共享契约",
            document,
            flowGraphShared,
            mafDefinition,
            tasks);
        ReferencesStayUnder(
            compilation,
            "Kuroe.Maf.Compilation",
            "流程共享契约与执行器共享层",
            document,
            flowGraphShared,
            flowCompilationShared,
            mafCompilationShared,
            mafDefinition,
            tasks,
            execution,
            executor);
    }

    [Fact(DisplayName = "流程区域不含 Maf")]
    public void Flow_area_stays_free_of_maf()
    {
        string flow = Path.Combine(SourceRoot, "Kuroe.Flow");
        string maf = Path.Combine(SourceRoot, "Kuroe.Maf");

        Assert.NotEmpty(ProjectFiles(flow));
        foreach (string project in ProjectFiles(flow))
        {
            Assert.DoesNotContain(
                ProjectReferences(project),
                reference => IsUnder(reference, maf));
        }
    }
}

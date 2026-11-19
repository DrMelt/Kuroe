using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryAssertions;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>MAF 边界：运行期层只依赖契约层与自己的记录定义，文档装配层只引用流程共享契约与描述文档。</summary>
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
        string definition = Path.Combine(SourceRoot, "Kuroe.Maf", "Definition", "Core");

        ReferencesStayUnder(project, "Kuroe.Maf.Definition", "执行共享与任务共享", execution, tasks);

        foreach (string file in SourceFiles(definition))
        {
            string text = File.ReadAllText(file);

            Assert.DoesNotContain("Microsoft.Agents.AI.Workflows", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Kuroe.Control", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Kuroe.Presentation", text, StringComparison.Ordinal);
        }
    }

    [Fact(DisplayName = "Maf 类型不出现在 Kernel 与宿主")]
    public void Maf_types_stay_out_of_kernel_and_hosts()
    {
        Assert.NotEmpty(SourceFiles(KernelRoot));
        foreach (string file in SourceFiles(KernelRoot))
        {
            string text = File.ReadAllText(file);

            Assert.DoesNotContain("Microsoft.Agents.AI.Workflows", text, StringComparison.Ordinal);
            Assert.DoesNotContain("MafFlow", text, StringComparison.Ordinal);
        }

        string[] hosts =
        [
            Path.Combine(SourceRoot, "Kuroe.Console"),
            Path.Combine(SourceRoot, "Kuroe.Cli"),
            Path.Combine(SourceRoot, "Kuroe", "Assistant", "Core"),
        ];

        foreach (string directory in hosts)
        {
            Assert.NotEmpty(SourceFiles(directory));
            foreach (string file in SourceFiles(directory))
            {
                Assert.DoesNotContain(
                    "using Kuroe.Maf.Control",
                    File.ReadAllText(file),
                    StringComparison.Ordinal);
            }
        }
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
        string document = Path.Combine(SourceRoot, "Kuroe.Document", "Shared");
        string flowGraphShared = Path.Combine(SourceRoot, "Kuroe.Flow", "Graph", "Shared");
        string flowCompilationShared = Path.Combine(SourceRoot, "Kuroe.Flow", "Compilation", "Shared");
        string mafCompilationShared = Path.Combine(SourceRoot, "Kuroe.Maf", "Compilation", "Shared");
        string mafDefinition = Path.Combine(SourceRoot, "Kuroe.Maf", "Definition", "Core");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string execution = Path.Combine(KernelRoot, "Execution", "Shared");

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
            "流程共享契约",
            document,
            flowGraphShared,
            flowCompilationShared,
            mafCompilationShared,
            mafDefinition,
            tasks,
            execution);
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

        foreach (string file in SourceFiles(flow))
        {
            string text = File.ReadAllText(file);

            Assert.DoesNotContain("MafFlow", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Microsoft.Agents.AI.Workflows", text, StringComparison.Ordinal);
        }
    }
}

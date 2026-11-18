using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryAssertions;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>Maf 边界：Maf 项目只依赖契约层与自己的记录定义，与流程区域互不引用。</summary>
public sealed class MafBoundaryTests
{
    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public void Maf_projects_stay_free_of_flow_area()
    {
        string maf = Path.Combine(SourceRoot, "Kuroe.Maf");
        string flow = Path.Combine(SourceRoot, "Kuroe.Flow");

        Assert.NotEmpty(ProjectFiles(maf));
        foreach (string project in ProjectFiles(maf))
        {
            Assert.DoesNotContain(
                ProjectReferences(project),
                reference => IsUnder(reference, flow));
        }
    }

    [Fact]
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

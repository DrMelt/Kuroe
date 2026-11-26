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
            KuroeRoot,
            "Maf",
            "Control",
            "Core",
            "Kuroe.Maf.Control.csproj");
        string control = Path.Combine(KernelRoot, "Control", "Shared");
        string execution = Path.Combine(KernelRoot, "Execution", "Shared");
        string executionRecords = Path.Combine(KernelRoot, "Execution", "Records", "Shared");
        string presentation = Path.Combine(KernelRoot, "Presentation", "Shared");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string taskRecords = Path.Combine(KernelRoot, "Tasks", "Records", "Shared");
        string mafRecords = Path.Combine(KuroeRoot, "Maf", "Records", "Shared");
        string mafDefinition = Path.Combine(KuroeRoot, "Maf", "Definition", "Core");

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
            KuroeRoot,
            "Maf",
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
            Path.Combine(KuroeRoot, "Host", "FlowIntegration", "Maf", "Core"),
            Path.Combine(KuroeRoot, "Maf", "Control", "Core"),
            Path.Combine(KuroeRoot, "Maf", "Definition", "Core"),
            Path.Combine(KuroeRoot, "Maf", "Persistence", "Core"),
            Path.Combine(KuroeRoot, "Maf", "Records", "Shared"),
        ];
        string flow = Path.Combine(KuroeRoot, "Flow");

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

    [Fact(DisplayName = "Maf 文档装配只引用流程描述")]
    public void Maf_compilation_references_only_flow_description()
    {
        string description = Path.Combine(KuroeRoot, "FlowDescription", "Description", "Shared");
        string definition = Path.Combine(KuroeRoot, "FlowDescription", "Definition", "Shared");
        string descriptionCompilation = Path.Combine(KuroeRoot, "FlowDescription", "Compilation", "Shared");
        string semanticsShared = Path.Combine(KuroeRoot, "FlowDescription", "Semantics", "Shared");
        string mafCompilationShared = Path.Combine(KuroeRoot, "Maf", "Compilation", "Shared");
        string mafDefinition = Path.Combine(KuroeRoot, "Maf", "Definition", "Core");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string execution = Path.Combine(KernelRoot, "Execution", "Shared");
        string flow = Path.Combine(KuroeRoot, "Flow");

        string shared = Path.Combine(
            KuroeRoot,
            "Maf",
            "Compilation",
            "Shared",
            "Kuroe.Maf.Compilation.Shared.csproj");
        string compilation = Path.Combine(
            KuroeRoot,
            "Maf",
            "Compilation",
            "Core",
            "Kuroe.Maf.Compilation.csproj");

        ReferencesStayUnder(
            shared,
            "Kuroe.Maf.Compilation.Shared",
            "流程描述、MAF 定义与执行契约",
            description,
            definition,
            mafDefinition,
            tasks,
            execution);
        ReferencesStayUnder(
            compilation,
            "Kuroe.Maf.Compilation",
            "流程描述、MAF 定义与 Kernel 契约",
            description,
            definition,
            descriptionCompilation,
            semanticsShared,
            mafCompilationShared,
            mafDefinition,
            tasks,
            execution);
        ReferencesExclude(compilation, "Kuroe.Maf.Compilation", "流程区域", flow);
    }

    [Fact(DisplayName = "流程区域不含 Maf")]
    public void Flow_area_stays_free_of_maf()
    {
        string flow = Path.Combine(KuroeRoot, "Flow");
        string maf = Path.Combine(KuroeRoot, "Maf");

        Assert.NotEmpty(ProjectFiles(flow));
        foreach (string project in ProjectFiles(flow))
        {
            Assert.DoesNotContain(
                ProjectReferences(project),
                reference => IsUnder(reference, maf));
        }
    }

    [Fact(DisplayName = "Maf 流程实现不引用图流程区域")]
    public void Maf_flow_area_stays_free_of_graph_flow_area()
    {
        string maf = Path.Combine(KuroeRoot, "Maf");
        string flow = Path.Combine(KuroeRoot, "Flow");

        Assert.NotEmpty(ProjectFiles(maf));
        foreach (string project in ProjectFiles(maf))
        {
            Assert.DoesNotContain(
                ProjectReferences(project),
                reference => IsUnder(reference, flow));
        }
    }

    [Fact(DisplayName = "Maf 装配不引用宿主装配")]
    public void Maf_assembly_stays_free_of_host_assemblies()
    {
        string[] assembly =
        [
            Path.Combine(KuroeRoot, "Host", "FlowIntegration", "Maf", "Core"),
        ];
        string[] host =
        [
            Path.Combine(KuroeRoot, "Host", "Composition"),
            Path.Combine(KuroeRoot, "Host", "FlowIntegration", "GraphFlow"),
            Path.Combine(KuroeRoot, "Host", "Assistant"),
        ];

        foreach (string directory in assembly)
        {
            IReadOnlyList<string> projects = [.. ProjectFiles(directory)];

            Assert.NotEmpty(projects);
            foreach (string project in projects)
            {
                Assert.DoesNotContain(
                    ProjectReferences(project),
                    reference => host.Any(target => IsUnder(reference, target)));
            }
        }
    }
}

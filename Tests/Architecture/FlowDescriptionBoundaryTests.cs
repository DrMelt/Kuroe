using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryAssertions;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>流程描述边界：流程描述共享层只引用任务契约，编译层只引用流程描述与任务契约，JSON 格式层只引用流程描述与契约层。</summary>
public sealed class FlowDescriptionBoundaryTests
{
    [Fact(DisplayName = "流程描述共享层只引用任务契约")]
    public void Flow_description_references_only_task_contracts()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.FlowDescription",
            "Shared",
            "Kuroe.FlowDescription.Shared.csproj");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");

        ReferencesStayUnder(project, "Kuroe.FlowDescription.Shared", "任务契约", tasks);
    }

    [Fact(DisplayName = "流程描述编译层只引用流程描述与任务契约")]
    public void Flow_description_compilation_references_only_description_and_task_contracts()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.FlowDescription",
            "Compilation",
            "Core",
            "Kuroe.FlowDescription.Compilation.csproj");
        string description = Path.Combine(SourceRoot, "Kuroe.FlowDescription", "Shared");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string flow = Path.Combine(SourceRoot, "Kuroe.Flow");
        string maf = Path.Combine(SourceRoot, "Kuroe.Maf");

        ReferencesStayUnder(project, "Kuroe.FlowDescription.Compilation", "流程描述与任务契约", description, tasks);
        ReferencesExclude(project, "Kuroe.FlowDescription.Compilation", "流程与 MAF 区域", flow, maf);
    }

    [Fact(DisplayName = "流程描述 JSON 共享层只引用流程描述")]
    public void Flow_description_json_shared_references_only_flow_descriptions()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.FlowDescription",
            "Json",
            "Shared",
            "Kuroe.FlowDescription.Json.Shared.csproj");
        string description = Path.Combine(SourceRoot, "Kuroe.FlowDescription", "Shared");

        ReferencesStayUnder(project, "流程描述 JSON 共享层", "流程描述", description);
    }

    [Fact(DisplayName = "流程描述 JSON 层只引用流程描述与契约层")]
    public void Flow_description_json_references_only_flow_descriptions_and_contracts()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.FlowDescription",
            "Json",
            "Core",
            "Kuroe.FlowDescription.Json.csproj");
        string description = Path.Combine(SourceRoot, "Kuroe.FlowDescription", "Shared");
        string jsonShared = Path.Combine(SourceRoot, "Kuroe.FlowDescription", "Json", "Shared");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string flow = Path.Combine(SourceRoot, "Kuroe.Flow");

        ReferencesStayUnder(project, "流程描述 JSON 层", "流程描述、JSON 共享层与 Kernel 契约", description, jsonShared, tasks);
        ReferencesExclude(project, "流程描述 JSON 层", "流程区域", flow);
    }

}
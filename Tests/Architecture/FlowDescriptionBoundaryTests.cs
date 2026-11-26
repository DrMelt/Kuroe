using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryAssertions;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>流程描述边界：描述、定义与编译三个共享层逐层依赖，JSON 层只依赖描述层。</summary>
public sealed class FlowDescriptionBoundaryTests
{
    private static readonly string DescriptionShared =
        Path.Combine(KuroeRoot, "FlowDescription", "Description", "Shared");

    private static readonly string DefinitionShared =
        Path.Combine(KuroeRoot, "FlowDescription", "Definition", "Shared");

    private static readonly string CompilationShared =
        Path.Combine(KuroeRoot, "FlowDescription", "Compilation", "Shared");

    private static readonly string SemanticsShared =
        Path.Combine(KuroeRoot, "FlowDescription", "Semantics", "Shared");

    private static readonly string Semantics =
        Path.Combine(KuroeRoot, "FlowDescription", "Semantics", "Core");

    private static readonly string Tasks = Path.Combine(KernelRoot, "Tasks", "Shared");

    [Fact(DisplayName = "流程描述共享层只引用任务契约")]
    public void Description_shared_references_only_task_contracts()
    {
        string project = Project(DescriptionShared, "Kuroe.FlowDescription.Description.Shared.csproj");

        ReferencesStayUnder(project, "Kuroe.FlowDescription.Description.Shared", "任务契约", Tasks);
    }

    [Fact(DisplayName = "流程定义共享层只引用描述共享层与任务契约")]
    public void Definition_shared_references_only_description_shared_and_task_contracts()
    {
        string project = Project(DefinitionShared, "Kuroe.FlowDescription.Definition.Shared.csproj");

        ReferencesStayUnder(
            project,
            "Kuroe.FlowDescription.Definition.Shared",
            "描述共享层与任务契约",
            DescriptionShared,
            Tasks);
    }

    [Fact(DisplayName = "流程描述编译共享层只引用描述与定义共享层")]
    public void Compilation_shared_references_only_description_and_definition_shared()
    {
        string project = Project(CompilationShared, "Kuroe.FlowDescription.Compilation.Shared.csproj");

        ReferencesStayUnder(
            project,
            "Kuroe.FlowDescription.Compilation.Shared",
            "描述、定义、语义共享层与任务契约",
            DescriptionShared,
            DefinitionShared,
            SemanticsShared,
            Tasks);
    }

    [Fact(DisplayName = "流程描述编译层只引用三个共享层与任务契约")]
    public void Flow_description_compilation_references_only_shared_description_layers()
    {
        string project = Path.Combine(
            KuroeRoot,
            "FlowDescription",
            "Compilation",
            "Core",
            "Kuroe.FlowDescription.Compilation.csproj");
        string flow = Path.Combine(KuroeRoot, "Flow");
        string maf = Path.Combine(KuroeRoot, "Maf");

        ReferencesStayUnder(
            project,
            "Kuroe.FlowDescription.Compilation",
            "描述、定义、语义共享、编译共享层与任务契约",
            DescriptionShared,
            DefinitionShared,
            SemanticsShared,
            CompilationShared,
            Tasks);
        ReferencesExclude(project, "Kuroe.FlowDescription.Compilation", "流程与 MAF 区域", flow, maf);
    }

    [Fact(DisplayName = "流程语义共享层只引用定义共享层与任务契约")]
    public void Semantics_shared_references_only_definition_shared_and_task_contracts()
    {
        string project = Project(SemanticsShared, "Kuroe.FlowDescription.Semantics.Shared.csproj");

        ReferencesStayUnder(
            project,
            "Kuroe.FlowDescription.Semantics.Shared",
            "定义共享层与任务契约",
            DefinitionShared,
            Tasks);
    }

    [Fact(DisplayName = "流程语义层只引用定义与语义共享层")]
    public void Semantics_references_only_shared_definition_layers()
    {
        string project = Project(Semantics, "Kuroe.FlowDescription.Semantics.csproj");

        ReferencesStayUnder(
            project,
            "Kuroe.FlowDescription.Semantics",
            "定义、语义共享层与任务契约",
            DefinitionShared,
            SemanticsShared,
            Tasks);
    }

    [Fact(DisplayName = "流程描述 JSON 共享层只引用描述共享层")]
    public void Flow_description_json_shared_references_only_description_shared()
    {
        string project = Path.Combine(
            KuroeRoot,
            "FlowDescription",
            "Json",
            "Shared",
            "Kuroe.FlowDescription.Json.Shared.csproj");

        ReferencesStayUnder(project, "流程描述 JSON 共享层", "描述共享层", DescriptionShared);
    }

    [Fact(DisplayName = "流程描述 JSON 层只引用描述共享层与格式契约")]
    public void Flow_description_json_references_only_description_shared_and_format_contracts()
    {
        string project = Path.Combine(
            KuroeRoot,
            "FlowDescription",
            "Json",
            "Core",
            "Kuroe.FlowDescription.Json.csproj");
        string jsonShared = Path.Combine(KuroeRoot, "FlowDescription", "Json", "Shared");
        string flow = Path.Combine(KuroeRoot, "Flow");
        string definition = DefinitionShared;

        ReferencesStayUnder(
            project,
            "流程描述 JSON 层",
            "描述共享层、格式契约与任务契约",
            DescriptionShared,
            jsonShared,
            Tasks);
        ReferencesExclude(project, "流程描述 JSON 层", "定义共享层与流程区域", definition, flow);
    }

    private static string Project(string directory, string name) => Path.Combine(directory, name);
}
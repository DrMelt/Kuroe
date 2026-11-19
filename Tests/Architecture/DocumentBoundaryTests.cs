using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryAssertions;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>描述文档边界：文档共享层不引用其他工程，JSON 格式层只引用描述文档与契约层，对流程实现保持中立。</summary>
public sealed class DocumentBoundaryTests
{
    [Fact(DisplayName = "描述文档不引用其他工程")]
    public void Document_references_no_other_project()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Document",
            "Shared",
            "Kuroe.Document.Shared.csproj");

        Assert.Empty(ProjectReferences(project));
    }

    [Fact(DisplayName = "描述文档 JSON 共享层只引用描述文档")]
    public void Document_json_shared_references_only_description_documents()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Document",
            "Json",
            "Shared",
            "Kuroe.Document.Json.Shared.csproj");
        string document = Path.Combine(SourceRoot, "Kuroe.Document", "Shared");

        ReferencesStayUnder(project, "描述文档 JSON 共享层", "描述文档", document);
    }

    [Fact(DisplayName = "描述文档 JSON 层只引用描述文档与契约层")]
    public void Document_json_references_only_description_documents_and_contracts()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Document",
            "Json",
            "Core",
            "Kuroe.Document.Json.csproj");
        string document = Path.Combine(SourceRoot, "Kuroe.Document", "Shared");
        string jsonShared = Path.Combine(SourceRoot, "Kuroe.Document", "Json", "Shared");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string flow = Path.Combine(SourceRoot, "Kuroe.Flow");

        ReferencesStayUnder(project, "描述文档 JSON 层", "描述文档、JSON 共享层与 Kernel 契约", document, jsonShared, tasks);
        ReferencesExclude(project, "描述文档 JSON 层", "流程区域", flow);
    }

    [Fact(DisplayName = "描述文档不含流程实现")]
    public void Document_stay_free_of_flow_implementation()
    {
        string document = Path.Combine(SourceRoot, "Kuroe.Document");
        string[] forbidden =
        [
            "Kuroe.Flow.Compilation",
            "Kuroe.Flow.Control",
            "Kuroe.Flow.Hosting",
            "Kuroe.Maf",
            "Microsoft.Agents",
        ];

        SourcesExclude(document, "流程实现", forbidden);
    }
}
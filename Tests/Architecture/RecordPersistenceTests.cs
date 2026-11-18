using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryAssertions;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>记录与持久化边界：记录种类的常量与编解码器随实现存放，任务持久化不出现流程模型。</summary>
public sealed class RecordPersistenceTests
{
    [Fact]
    public void Graph_records_project_stays_free_of_runtime_and_host()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Graph",
            "Records",
            "Shared",
            "Kuroe.Flow.Graph.Records.Shared.csproj");
        string graph = Path.Combine(SourceRoot, "Kuroe.Flow", "Shared");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string tasksRecords = Path.Combine(KernelRoot, "Tasks", "Records", "Shared");
        string execution = Path.Combine(KernelRoot, "Execution", "Shared");
        string nodes = Path.Combine(SourceRoot, "Kuroe.Flow", "Node", "Shared");

        ReferencesStayUnder(
            project,
            Path.GetFileName(project),
            "图记录边界",
            graph,
            tasks,
            tasksRecords,
            execution,
            nodes);
    }

    [Fact]
    public void Persistence_projects_reference_only_shared_boundaries()
    {
        string persistence = Path.Combine(KernelRoot, "Tasks", "Persistence");
        string taskContracts = Path.Combine(KernelRoot, "Tasks", "Shared");
        string taskPresentation = Path.Combine(KernelRoot, "Tasks", "Presentation", "Shared");
        string executionContracts = Path.Combine(KernelRoot, "Execution", "Shared");
        string executionRecords = Path.Combine(KernelRoot, "Execution", "Records", "Shared");
        string taskRecords = Path.Combine(KernelRoot, "Tasks", "Records", "Shared");

        foreach (string project in ProjectFiles(persistence))
        {
            IReadOnlyList<string> references = [.. ProjectReferences(project)
                .Where(reference => IsUnder(reference, SourceRoot))];

            Assert.All(references, reference =>
                Assert.True(
                    IsPersistenceBoundary(reference, persistence)
                        || IsUnder(reference, taskContracts)
                        || IsUnder(reference, taskPresentation)
                        || IsUnder(reference, executionContracts)
                        || IsUnder(reference, executionRecords)
                        || IsUnder(reference, taskRecords),
                    $"{Path.GetFileName(project)} 引用了持久化边界之外的 {reference}。"));
        }
    }

    [Fact]
    public void Task_persistence_projects_stay_free_of_flow_models()
    {
        string persistence = Path.Combine(KernelRoot, "Tasks", "Persistence");
        string[] projects =
        [
            Path.Combine(persistence, "Files", "Shared", "Kuroe.Tasks.Persistence.Files.Shared.csproj"),
            Path.Combine(persistence, "Files", "Core", "Kuroe.Tasks.Persistence.Files.csproj"),
            Path.Combine(persistence, "Restoring", "Shared", "Kuroe.Tasks.Persistence.Restoring.Shared.csproj"),
            Path.Combine(persistence, "Restoring", "Core", "Kuroe.Tasks.Persistence.Restoring.csproj"),
            Path.Combine(persistence, "Saving", "Shared", "Kuroe.Tasks.Persistence.Saving.Shared.csproj"),
            Path.Combine(persistence, "Saving", "Core", "Kuroe.Tasks.Persistence.Saving.csproj"),
            Path.Combine(
                persistence,
                "Serialization",
                "Shared",
                "Kuroe.Tasks.Persistence.Serialization.Shared.csproj"),
            Path.Combine(
                persistence,
                "Serialization",
                "Json",
                "Core",
                "Kuroe.Tasks.Persistence.Json.csproj"),
        ];
        string graphRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Graph", "Records", "Shared");
        string nodeContracts = Path.Combine(SourceRoot, "Kuroe.Flow", "Node", "Shared");
        string flowContracts = Path.Combine(SourceRoot, "Kuroe.Flow", "Shared");
        string[] forbidden =
        [
            "Kuroe.Flow.Node",
            "Kuroe.Flow.Graph",
            "Kuroe.Flow.Shared",
            "GraphFlowDefinitionRecord",
            "CodeFlowDefinitionRecord",
        ];

        foreach (string project in projects)
        {
            ReferencesExclude(
                project,
                Path.GetFileName(project),
                "图或节点模型",
                graphRecords,
                nodeContracts,
                flowContracts);
            SourcesExclude(DirectoryOf(project), "图或节点词汇", forbidden);
        }
    }

    [Fact]
    public void Flow_record_codecs_live_outside_central_serialization()
    {
        string persistence = Path.Combine(KernelRoot, "Tasks", "Persistence", "Serialization");
        string graphCodec = Path.Combine(SourceRoot, "Kuroe.Flow", "Persistence", "Serialization", "Graph", "Core");
        string code = Path.Combine(SourceRoot, "Kuroe.CodeFlow", "Persistence", "Core");

        Assert.True(File.Exists(Path.Combine(graphCodec, "GraphFlowRecordCodec.cs")));
        Assert.True(File.Exists(Path.Combine(code, "CodeFlowRecordCodec.cs")));
        Assert.False(File.Exists(Path.Combine(persistence, "Shared", "FlowRecordCodec.cs")));
    }

    [Fact]
    public void Flow_record_kind_constants_live_with_implementations()
    {
        string graph = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Graph",
            "Records",
            "Shared",
            "GraphFlowKind.cs");
        string code = Path.Combine(
            SourceRoot,
            "Kuroe.CodeFlow",
            "Records",
            "Shared",
            "CodeFlowKind.cs");
        string taskRecord = Path.Combine(KernelRoot, "Tasks", "Records", "Shared", "TaskRecord.cs");

        Assert.True(File.Exists(graph));
        Assert.True(File.Exists(code));
        Assert.DoesNotContain("FlowRecordKinds", File.ReadAllText(taskRecord), StringComparison.Ordinal);
    }

    [Fact]
    public void Maf_record_kind_lives_with_its_implementation()
    {
        string kind = Path.Combine(
            SourceRoot,
            "Kuroe.Maf",
            "Records",
            "Shared",
            "MafFlowKind.cs");
        string taskRecord = Path.Combine(KernelRoot, "Tasks", "Records", "Shared", "TaskRecord.cs");

        Assert.True(File.Exists(kind));
        Assert.DoesNotContain("MafFlowKind", File.ReadAllText(taskRecord), StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_record_codec_port_lives_in_kernel()
    {
        string port = Path.Combine(KernelRoot, "Tasks", "Records", "Shared", "FlowRecordCodec.cs");
        string serialization = Path.Combine(SourceRoot, "Kuroe.Kernel", "Tasks", "Persistence", "Serialization", "Shared");

        Assert.True(File.Exists(port));
        Assert.Contains("interface IFlowRecordCodec", File.ReadAllText(port), StringComparison.Ordinal);
        Assert.DoesNotContain(
            "interface IFlowRecordCodec",
            string.Concat(SourceFiles(serialization).Select(File.ReadAllText)),
            StringComparison.Ordinal);
    }

    private static bool IsPersistenceBoundary(string project, string persistence)
    {
        string directory = DirectoryOf(project);

        return IsUnder(directory, persistence)
            && string.Equals(Path.GetFileName(directory), "Shared", StringComparison.Ordinal);
    }
}

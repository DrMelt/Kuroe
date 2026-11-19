using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryAssertions;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>记录与持久化边界：记录种类的常量与编解码器随实现存放，任务持久化不出现流程模型。</summary>
public sealed class RecordPersistenceTests
{
    [Fact(DisplayName = "图记录项目不含运行期与宿主")]
    public void Graph_records_project_stays_free_of_runtime_and_host()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Records",
            "Shared",
            "Kuroe.Flow.Records.Shared.csproj");
        string nodes = Path.Combine(SourceRoot, "Kuroe.Flow", "Graph", "Shared");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string tasksRecords = Path.Combine(KernelRoot, "Tasks", "Records", "Shared");
        string execution = Path.Combine(KernelRoot, "Execution", "Shared");
        string description = Path.Combine(SourceRoot, "Kuroe.FlowDescription", "Shared");

        ReferencesStayUnder(
            project,
            Path.GetFileName(project),
            "图记录边界",
            nodes,
            tasks,
            tasksRecords,
            execution,
            description);
    }

    [Fact(DisplayName = "持久化项目只引用共享边界")]
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

    [Fact(DisplayName = "任务持久化项目不含流程模型")]
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
        string graphRecords = Path.Combine(SourceRoot, "Kuroe.Flow", "Records", "Shared");
        string nodeContracts = Path.Combine(SourceRoot, "Kuroe.Flow", "Graph", "Shared");

        foreach (string project in projects)
        {
            ReferencesExclude(
                project,
                Path.GetFileName(project),
                "图或节点模型",
                graphRecords,
                nodeContracts);
        }
    }

    [Fact(DisplayName = "流程记录编解码器位于集中序列化之外")]
    public void Flow_record_codecs_live_outside_central_serialization()
    {
        string persistence = Path.Combine(KernelRoot, "Tasks", "Persistence", "Serialization");
        string graphCodec = Path.Combine(SourceRoot, "Kuroe.Flow", "Persistence", "Serialization", "Graph", "Core");
        string code = Path.Combine(SourceRoot, "Kuroe.CodeFlow", "Persistence", "Core");

        Assert.True(File.Exists(Path.Combine(graphCodec, "GraphFlowRecordCodec.cs")), $"{Path.Combine(graphCodec, "GraphFlowRecordCodec.cs")} 不存在。");
        Assert.True(File.Exists(Path.Combine(code, "CodeFlowRecordCodec.cs")), $"{Path.Combine(code, "CodeFlowRecordCodec.cs")} 不存在。");
        Assert.False(File.Exists(Path.Combine(persistence, "Shared", "FlowRecordCodec.cs")), $"{Path.Combine(persistence, "Shared", "FlowRecordCodec.cs")} 不应存在。");
    }

    [Fact(DisplayName = "流程记录种类常量随实现存放")]
    public void Flow_record_kind_constants_live_with_implementations()
    {
        string graph = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Records",
            "Shared",
            "GraphFlowKind.cs");
        string code = Path.Combine(
            SourceRoot,
            "Kuroe.CodeFlow",
            "Records",
            "Shared",
            "CodeFlowKind.cs");
        Assert.True(File.Exists(graph), $"{graph} 不存在。");
        Assert.True(File.Exists(code), $"{code} 不存在。");
    }

    [Fact(DisplayName = "Maf 记录种类随其实现存放")]
    public void Maf_record_kind_lives_with_its_implementation()
    {
        string kind = Path.Combine(
            SourceRoot,
            "Kuroe.Maf",
            "Records",
            "Shared",
            "MafFlowKind.cs");
        Assert.True(File.Exists(kind), $"{kind} 不存在。");
    }

    [Fact(DisplayName = "流程记录编解码端口位于 Kernel")]
    public void Flow_record_codec_port_lives_in_kernel()
    {
        string port = Path.Combine(KernelRoot, "Tasks", "Records", "Shared", "FlowRecordCodec.cs");

        Assert.True(File.Exists(port), $"{port} 不存在。");
        Assert.Contains("interface IFlowRecordCodec", File.ReadAllText(port), StringComparison.Ordinal);
    }

    private static bool IsPersistenceBoundary(string project, string persistence)
    {
        string directory = DirectoryOf(project);

        return IsUnder(directory, persistence)
            && string.Equals(Path.GetFileName(directory), "Shared", StringComparison.Ordinal);
    }
}

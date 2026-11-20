using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>宿主与工具边界：除 Console 外宿主不引用图运行期与图共享层；Console 注册图节点能力，只额外引用图共享层与节点执行装配。
/// 工具共享项不依赖同模块核心与文件访问。</summary>
public sealed class HostAndToolBoundaryTests
{
    [Fact(DisplayName = "文件访问共享层不含核心实现与文件 IO")]
    public void FileAccess_shared_project_stays_free_of_core_and_file_io()
    {
        string fileAccess = Path.Combine(SourceRoot, "Kuroe", "Tool", "FileAccess");
        string shared = Path.Combine(fileAccess, "Shared", "Kuroe.Tool.FileAccess.Shared.csproj");
        string core = Path.Combine(fileAccess, "Core", "Kuroe.Tool.FileAccess.csproj");
        IReadOnlyList<string> sharedReferences = ProjectReferences(shared);

        Assert.DoesNotContain(sharedReferences, reference => IsUnder(reference, Path.Combine(fileAccess, "Core")));
        Assert.DoesNotContain(sharedReferences, reference =>
            IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Files")));
        Assert.Contains(shared, ProjectReferences(core));
    }

    [Fact(DisplayName = "除 Console 外宿主不引用图运行期与图共享层")]
    public void Hosts_do_not_reference_graph_runtime_or_node_shared()
    {
        string[] projects =
        [
            Path.Combine(SourceRoot, "Kuroe.Cli", "Kuroe.Cli.csproj"),
            Path.Combine(SourceRoot, "Kuroe", "Assistant", "Core", "Kuroe.Assistant.csproj"),
            Path.Combine(
                SourceRoot,
                "Kuroe",
                "Tool",
                "Implementation",
                "KuroeInfo",
                "Core",
                "Kuroe.Tool.Implementation.KuroeInfo.csproj"),
        ];

        foreach (string project in projects)
        {
            IReadOnlyList<string> references = ProjectReferences(project);

            Assert.DoesNotContain(references, reference =>
                IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Core")));
            Assert.DoesNotContain(references, reference =>
                IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow", "Graph", "Shared")));
        }
    }

    [Fact(DisplayName = "Console 只额外引用图共享层与图契约层")]
    public void Console_references_only_graph_shared_and_graph_contracts_from_flow()
    {
        string project = Path.Combine(SourceRoot, "Kuroe.Console", "Kuroe.Console.csproj");
        string flow = Path.Combine(SourceRoot, "Kuroe.Flow");
        string graphShared = Path.Combine(flow, "Graph", "Shared", "Kuroe.Flow.Graph.Shared.csproj");
        string control = Path.Combine(flow, "Control", "Core", "Kuroe.Flow.Control.csproj");
        string records = Path.Combine(flow, "Records", "Shared", "Kuroe.Flow.Records.Shared.csproj");
        string library = Path.Combine(flow, "Library", "Core", "Kuroe.Flow.Library.csproj");
        string graphFlow = Path.Combine(SourceRoot, "Kuroe", "GraphFlow", "Core", "Kuroe.GraphFlow.csproj");
        IReadOnlyList<string> references = ProjectReferences(project);

        Assert.Contains(graphShared, references);
        Assert.Contains(graphFlow, references);
        Assert.All(
            references.Where(reference => IsUnder(reference, flow)),
            reference => Assert.True(
                string.Equals(reference, graphShared, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(reference, control, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(reference, records, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(reference, library, StringComparison.OrdinalIgnoreCase),
                $"Console 引用了图区域的 {reference}。"));
    }
}

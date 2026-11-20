using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>宿主与工具边界：宿主不引用图运行期与图共享层，工具共享项不依赖同模块核心与文件访问。
/// Console 注册图节点能力，因此作为装配入口单独允许引用图共享层。</summary>
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

    [Fact(DisplayName = "宿主不引用图运行期，入口不引用图共享层")]
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
                "Kuroe.Tool.KuroeInfo.csproj"),
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

}

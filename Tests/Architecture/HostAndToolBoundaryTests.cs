using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryAssertions;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>宿主与工具边界：宿主与代码流程源码不导入图命名空间，宿主不引图运行期与节点共享，工具共享项不依赖同模块核心与文件访问。</summary>
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

    [Fact(DisplayName = "宿主不引用图运行期与节点共享层")]
    public void Hosts_do_not_reference_graph_runtime_or_node_shared()
    {
        string[] projects =
        [
            Path.Combine(SourceRoot, "Kuroe.Console", "Kuroe.Console.csproj"),
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
                IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Graph")));
            Assert.DoesNotContain(references, reference =>
                IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow", "Node", "Shared")));
            Assert.DoesNotContain(references, reference =>
                IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow", "Shared")));
        }
    }

    [Fact(DisplayName = "宿主与代码流程源码不导入图命名空间")]
    public void Hosts_and_code_flow_sources_do_not_import_graph_namespaces()
    {
        string[] directories =
        [
            Path.Combine(SourceRoot, "Kuroe.Console"),
            Path.Combine(SourceRoot, "Kuroe.Cli"),
            Path.Combine(SourceRoot, "Kuroe", "Assistant", "Core"),
            Path.Combine(SourceRoot, "Kuroe", "Tool", "Implementation", "KuroeInfo"),
        ];
        string[] forbidden =
        [
            "Kuroe.Flow.Node.StaticGraph",
            "Kuroe.Flow.Node.RuntimeGraph",
            "Kuroe.Flow.Control.Graph",
            "Kuroe.Flow.Node.Shared",
            "Kuroe.Flow.Graph.Records",
        ];

        foreach (string directory in directories)
        {
            SourcesExclude(directory, "图命名空间", forbidden);
        }
    }
}

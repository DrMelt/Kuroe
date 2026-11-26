using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>语义实现的构造边界：目标实现只使用语义契约，实现实例由宿主组合根提供。</summary>
public sealed class InjectionBoundaryTests
{
    [Fact(DisplayName = "流程实现不构造语义实现")]
    public void Flow_implementations_do_not_construct_semantics()
    {
        string[] roots =
        [
            Path.Combine(KuroeRoot, "Flow"),
            Path.Combine(KuroeRoot, "Maf"),
        ];

        foreach (string root in roots)
        {
            foreach (string file in SourceFiles(root))
            {
                Assert.DoesNotContain("new FlowSemanticsAnalyzer", File.ReadAllText(file));
            }
        }
    }

    [Fact(DisplayName = "宿主组合根提供语义实现")]
    public void Host_composition_provides_semantics()
    {
        string project = Path.Combine(
            KuroeRoot,
            "Host",
            "FlowIntegration",
            "GraphFlow",
            "Core",
            "GraphFlowServiceCollectionExtensions.cs");

        Assert.Contains("new FlowSemanticsAnalyzer", File.ReadAllText(project));
    }
}
using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>助手边界：对话宿主只依赖代码流程，不依赖图控制。</summary>
public sealed class AssistantBoundaryTests
{
    [Fact]
    public void Dialogue_host_depends_on_code_flow_not_graph_control()
    {
        string dialogue = Path.Combine(SourceRoot, "Kuroe", "Assistant", "Core", "Dialogue");
        IReadOnlyList<string> files = [.. SourceFiles(dialogue)];

        Assert.NotEmpty(files);
        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            Assert.DoesNotContain("GraphFlowFramework", text, StringComparison.Ordinal);
            Assert.DoesNotContain("PrepareCompiled", text, StringComparison.Ordinal);
            Assert.DoesNotContain("IFlowDescriptionCompiler", text, StringComparison.Ordinal);
            Assert.DoesNotContain("FlowDocument", text, StringComparison.Ordinal);
        }

        string project = Path.Combine(SourceRoot, "Kuroe", "Assistant", "Core", "Kuroe.Assistant.csproj");
        IReadOnlyList<string> references = ProjectReferences(project);

        Assert.DoesNotContain(references, reference =>
            IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Graph")));
        Assert.DoesNotContain(references, reference =>
            IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow", "Compilation", "Shared")));
    }

    [Fact]
    public void Assistant_separates_dialogue_hosting_from_its_code_flow()
    {
        string core = Path.Combine(SourceRoot, "Kuroe", "Assistant", "Core");
        string dialogue = Path.Combine(core, "Dialogue");
        string flow = Path.Combine(dialogue, "Flow");

        Assert.True(File.Exists(Path.Combine(dialogue, "DialogueHost.cs")));
        Assert.True(File.Exists(Path.Combine(dialogue, "DialogueRound.cs")));
        Assert.False(File.Exists(Path.Combine(dialogue, "DialogueFlowControl.cs")));
        Assert.True(File.Exists(Path.Combine(flow, "DialogueFlow.cs")));
        Assert.True(File.Exists(Path.Combine(flow, "DialogueFlowControl.cs")));
        Assert.True(File.Exists(Path.Combine(flow, "DialogueFlowView.cs")));
        Assert.False(File.Exists(Path.Combine(flow, "DialogueFlowTarget.cs")));
        Assert.False(Directory.Exists(Path.Combine(core, "Flows")));
    }

}

using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>助手边界：对话宿主只依赖代码流程，不依赖图控制。</summary>
public sealed class AssistantBoundaryTests
{
    [Fact(DisplayName = "对话宿主只依赖代码流程不依赖图控制")]
    public void Dialogue_host_depends_on_code_flow_not_graph_control()
    {
        string project = Path.Combine(SourceRoot, "Kuroe.Host", "Assistant", "Core", "Kuroe.Host.Assistant.csproj");
        IReadOnlyList<string> references = ProjectReferences(project);

        Assert.DoesNotContain(references, reference =>
            IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Core")));
        Assert.DoesNotContain(references, reference =>
            IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow", "Compilation", "Shared")));
        Assert.DoesNotContain(references, reference =>
            IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.FlowDescription")));
    }

    [Fact(DisplayName = "助手把对话宿主与其代码流程分开")]
    public void Assistant_separates_dialogue_hosting_from_its_code_flow()
    {
        string core = Path.Combine(SourceRoot, "Kuroe.Host", "Assistant", "Core");
        string dialogue = Path.Combine(core, "Dialogue");
        string flow = Path.Combine(dialogue, "Flow");

        Assert.True(File.Exists(Path.Combine(dialogue, "DialogueHost.cs")), $"{Path.Combine(dialogue, "DialogueHost.cs")} 不存在。");
        Assert.True(File.Exists(Path.Combine(dialogue, "DialogueRound.cs")), $"{Path.Combine(dialogue, "DialogueRound.cs")} 不存在。");
        Assert.False(File.Exists(Path.Combine(dialogue, "DialogueFlowControl.cs")), $"{Path.Combine(dialogue, "DialogueFlowControl.cs")} 不应存在。");
        Assert.True(File.Exists(Path.Combine(flow, "DialogueFlow.cs")), $"{Path.Combine(flow, "DialogueFlow.cs")} 不存在。");
        Assert.True(File.Exists(Path.Combine(flow, "DialogueFlowControl.cs")), $"{Path.Combine(flow, "DialogueFlowControl.cs")} 不存在。");
        Assert.True(File.Exists(Path.Combine(flow, "DialogueFlowView.cs")), $"{Path.Combine(flow, "DialogueFlowView.cs")} 不存在。");
        Assert.False(File.Exists(Path.Combine(flow, "DialogueFlowTarget.cs")), $"{Path.Combine(flow, "DialogueFlowTarget.cs")} 不应存在。");
        Assert.False(Directory.Exists(Path.Combine(core, "Flows")), $"{Path.Combine(core, "Flows")} 不应存在。");
    }

}

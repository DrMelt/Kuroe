using Kuroe.Executions.Tools;
using Kuroe.Executions.Turns;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Executions.Turns;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.Tools;
using Kuroe.Tools.KuroeTools;
using Kuroe.Workflows.FlowAssembly;
using Kuroe.Workflows.TaskExecution.Tasks;
using Microsoft.Extensions.AI;
using Xunit;

namespace Kuroe.Tests;

/// <summary>内置对话流程的节点配置与工具白名单在工具面上的应用。</summary>
public sealed class DialogueTests
{
    [Fact]
    public void Dialogue_flow_builds_input_and_text_reply_nodes()
    {
        IReadOnlyList<NodeSpec> nodes = DialogueFlow.Build().RootNode.Nodes!;

        Assert.Equal(NodeOutput.Input, nodes[0].Execution!.Output);
        Assert.Equal(NodeOutput.Text, nodes[1].Execution!.Output);
    }

    [Fact]
    public void Dialogue_flow_whitelists_dialogue_tools_by_path()
    {
        Assert.Equal(
            [
                new ToolPath("GetLocalTime"),
                new ToolPath("info"),
                new ToolPath("files"),
            ],
            DialogueFlow.Tools);
    }

    [Fact]
    public void Tool_face_filters_to_whitelist()
    {
        ToolCollection tools = new(
        [
            new TimeTool(),
            new TaskInfoTool(new TaskRegistry()),
            new SampleTool(),
        ]);
        TurnScope scope = Scope();

        IReadOnlyList<AITool> built = tools.Build(scope, new NullSink(), DialogueFlow.Tools);

        Assert.Equal(
            ["GetLocalTime", "ListTasks", "GetTask", "GetRun"],
            built.Select(tool => tool.Name));
    }

    [Fact]
    public void Whitelist_parent_path_grants_all_descendants()
    {
        ToolCollection tools = new(
        [
            new TimeTool(),
            new TaskInfoTool(new TaskRegistry()),
            new SampleTool(),
        ]);
        TurnScope scope = Scope();

        IReadOnlyList<AITool> built = tools.Build(scope, new NullSink(), [new ToolPath("info")]);

        Assert.Equal(
            ["ListTasks", "GetTask", "GetRun"],
            built.Select(tool => tool.Name));
    }

    [Fact]
    public void Whitelist_full_path_grants_only_that_function()
    {
        ToolCollection tools = new(
        [
            new TimeTool(),
            new TaskInfoTool(new TaskRegistry()),
        ]);
        TurnScope scope = Scope();

        IReadOnlyList<AITool> built = tools.Build(scope, new NullSink(), [new ToolPath("info/GetTask")]);

        Assert.Equal(["GetTask"], built.Select(tool => tool.Name));
    }

    [Fact]
    public void Whitelist_with_similar_prefix_does_not_match_group()
    {
        ToolCollection tools = new([new TaskInfoTool(new TaskRegistry())]);
        TurnScope scope = Scope();

        IReadOnlyList<AITool> built = tools.Build(scope, new NullSink(), [new ToolPath("infoX")]);

        Assert.Empty(built);
    }

    private static TurnScope Scope() => new()
    {
        Task = new TaskId(1),
        Run = new RunId(1),
        Output = NodeOutput.Text,
        NodeName = DialogueFlow.ReplyNodeName,
        ItemIndex = null,
        Journal = new TurnJournal(),
        Sink = new NullSink(),
    };

    private sealed class SampleTool : ITool
    {
        public IReadOnlyList<ToolFunction> Functions { get; } =
        [
            new ToolFunction(new ToolName("RunDiff"), "样例工具", [], _ => string.Empty),
        ];
    }

    private sealed class NullSink : ITurnSink
    {
        public void OnText(string delta)
        {
        }

        public void OnToolCall(ToolCallRecord record)
        {
        }
    }
}

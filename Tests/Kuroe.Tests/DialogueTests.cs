using Kuroe.Flow.Node.Input;
using Kuroe.Flow.Node.Llm.Tools;
using Kuroe.Flow.Execution.Turns;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Flow.Shared.Execution.Turns;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.Node.Llm;
using Kuroe.Workflows.Tools.Builtin;
using Kuroe.Workflows.Tools.Info;
using Kuroe.Workflows.Flows;
using Kuroe.Flow.Execution.Tasks;
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

        Assert.Equal(InputNodeTypes.Name, nodes[0].Kind);
        Assert.Equal(LlmNodeTypes.Name, nodes[1].Kind);
    }

    [Fact]
    public void Dialogue_flow_marks_reply_back_edge_as_signal()
    {
        IReadOnlyList<NodeSpec> nodes = DialogueFlow.Build().RootNode.Nodes!;

        Assert.Equal(PortType.Signal, nodes[0].Inputs.Find(DialogueFlow.ReplyPort)!.Value.Type);
        Assert.Equal(PortType.Text, nodes[1].Outputs.Find(DialogueFlow.ReplyPort)!.Value.Type);
    }

    [Fact]
    public void Dialogue_reply_node_declares_constant_rule_as_system_prompt()
    {
        NodeSpec reply = DialogueFlow.Build().RootNode.Nodes![1];
        LlmParameters parameters = Assert.IsType<LlmParameters>(reply.Parameters);

        Assert.Equal(["你是Kuroe的内置助手，直接回应用户的输入。回复内容写在正常输出里，端口交回按本轮接口说明执行。"], parameters.SystemPrompt);
        Assert.Null(parameters.Prompt);
    }

    [Fact]
    public void Dialogue_flow_whitelists_dialogue_tools_by_path()
    {
        Assert.Equal(
            [
                new CapabilityPath("GetLocalTime"),
                new CapabilityPath("info"),
                new CapabilityPath("files"),
                new CapabilityPath("source"),
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

        IReadOnlyList<AITool> built = tools.Build(scope, new NullSink(), [new CapabilityPath("info")]);

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

        IReadOnlyList<AITool> built = tools.Build(scope, new NullSink(), [new CapabilityPath("info/GetTask")]);

        Assert.Equal(["GetTask"], built.Select(tool => tool.Name));
    }

    [Fact]
    public void Whitelist_with_similar_prefix_does_not_match_group()
    {
        ToolCollection tools = new([new TaskInfoTool(new TaskRegistry())]);
        TurnScope scope = Scope();

        IReadOnlyList<AITool> built = tools.Build(scope, new NullSink(), [new CapabilityPath("infoX")]);

        Assert.Empty(built);
    }

    private static TurnScope Scope() => new()
    {
        Task = new TaskId(1),
        Run = new RunId(1),
        NodeName = DialogueFlow.ReplyNodeName,
        ItemIndex = null,
        Journal = new TurnJournal(),
        Sink = new NullSink(),
    };

    private sealed class SampleTool : ITool
    {
        public IReadOnlyList<ToolFunction> Functions { get; } =
        [
            new ToolDefinition(new ToolName("RunDiff"), "样例工具", []).Bind(_ => string.Empty),
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

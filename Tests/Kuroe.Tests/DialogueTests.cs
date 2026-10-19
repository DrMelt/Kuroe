using Kuroe.Node.Input;
using Kuroe.Node.Llm.Json;
using Kuroe.Node.Llm.Tools;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;
using Kuroe.Tool;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Node.Llm;
using Kuroe.Node.Llm.Executions;
using Kuroe.Tool.KuroeInfo;
using Kuroe.Tool.Time;
using Kuroe.Workflows.Flows;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.InputChannel;
using Microsoft.Extensions.AI;
using Xunit;

namespace Kuroe.Tests;

/// <summary>内置对话流程的节点配置与工具白名单在工具面上的应用。</summary>
public sealed class DialogueTests
{
    /// <summary>外部输入协议的宿主实现，只为建出流程。</summary>
    private static readonly InputRegistry Inputs = new();

    [Fact]
    public void Dialogue_flow_builds_input_and_text_reply_nodes()
    {
        IReadOnlyList<NodeSpec> nodes = DialogueFlow.Build(Inputs).RootNode.Nodes!;

        Assert.Equal(InputNodeTypes.Name, nodes[0].Kind);
        Assert.Equal(LlmNodeTypes.Name, nodes[1].Kind);
    }

    [Fact]
    public void Dialogue_flow_marks_reply_back_edge_as_signal()
    {
        IReadOnlyList<NodeSpec> nodes = DialogueFlow.Build(Inputs).RootNode.Nodes!;

        Assert.Equal(PortType.Signal, nodes[0].Inputs.Find(DialogueFlow.ReplyPort)!.Value.Type);
        Assert.Equal(PortType.Text, nodes[1].Outputs.Find(DialogueFlow.ReplyPort)!.Value.Type);
    }

    [Fact]
    public void Dialogue_reply_node_declares_constant_rule_as_system_prompt()
    {
        NodeSpec reply = DialogueFlow.Build(Inputs).RootNode.Nodes![1];
        LlmParameters parameters = Assert.IsType<LlmParameters>(Assert.IsType<LlmNodeType>(reply.Type).Parameters);

        Assert.Equal(["你是Kuroe的内置助手，直接回应用户的输入。回复内容写在正常输出里，端口交回按工具说明执行。"], parameters.SystemPrompt);
        Assert.Null(parameters.Prompt);
    }

    [Fact]
    public void Dialogue_flow_whitelists_dialogue_tools_by_path()
    {
        Assert.Equal(
            [
                new ToolPath("GetLocalTime"),
                new ToolPath("KuroeInfo"),
                new ToolPath("file"),
                new ToolPath("source"),
                new ToolPath("git/read"),
                new ToolPath("dotnet/read"),
            ],
            DialogueFlow.Tools);
    }

    [Fact]
    public void Tool_face_filters_to_whitelist()
    {
        ToolCollection tools = new(
        [
            new TimeTool(),
            new TaskKuroeInfoTool(new TaskRegistry()),
            new SampleTool(),
        ],
        new ToolJson());
        IReadOnlyList<AITool> built = tools.Build(new ExecutionId(1), NoSink, new ProcessJournal(), DialogueFlow.Tools);

        Assert.Equal(
            ["GetLocalTime", "KuroeInfo/ListTasks", "KuroeInfo/GetTask", "KuroeInfo/GetExecution"],
            built.Select(tool => tool.Name));
    }

    [Fact]
    public void Whitelist_parent_path_grants_all_descendants()
    {
        ToolCollection tools = new(
        [
            new TimeTool(),
            new TaskKuroeInfoTool(new TaskRegistry()),
            new SampleTool(),
        ],
        new ToolJson());
        IReadOnlyList<AITool> built = tools.Build(new ExecutionId(1), NoSink, new ProcessJournal(), [new ToolPath("KuroeInfo")]);

        Assert.Equal(
            ["KuroeInfo/ListTasks", "KuroeInfo/GetTask", "KuroeInfo/GetExecution"],
            built.Select(tool => tool.Name));
    }

    [Fact]
    public void Whitelist_full_path_grants_only_that_function()
    {
        ToolCollection tools = new(
        [
            new TimeTool(),
            new TaskKuroeInfoTool(new TaskRegistry()),
        ],
        new ToolJson());
        IReadOnlyList<AITool> built = tools.Build(new ExecutionId(1), NoSink, new ProcessJournal(), [new ToolPath("KuroeInfo/GetTask")]);

        Assert.Equal(["KuroeInfo/GetTask"], built.Select(tool => tool.Name));
    }

    [Fact]
    public void Whitelist_with_similar_prefix_does_not_match_group()
    {
        ToolCollection tools = new([new TaskKuroeInfoTool(new TaskRegistry())], new ToolJson());
        IReadOnlyList<AITool> built = tools.Build(new ExecutionId(1), NoSink, new ProcessJournal(), [new ToolPath("KuroeInfoX")]);

        Assert.Empty(built);
    }

    /// <summary>工具面构造只取身份、播发出口与记录，本组测试不看过程内容。</summary>
    private static readonly NullSink NoSink = new();

    private sealed class SampleTool : ITool
    {
        public IReadOnlyList<ToolFunction> Functions { get; } =
        [
            new ToolDefinition(new ToolPath("RunDiff"), "样例工具", []).Bind(_ => string.Empty),
        ];
    }

    private sealed class NullSink : IProcessSink
    {
        public void OnText(string delta)
        {
        }

        public void OnToolCall(ToolCallRecord record)
        {
        }
    }
}

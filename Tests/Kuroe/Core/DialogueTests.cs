using Kuroe.Configuration.Model;
using Kuroe.Node.Input;
using Kuroe.Node.Llm.Tools;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Tool;
using Kuroe.Flow.Compiler;
using Kuroe.Flow.Compiler.Shared;
using Kuroe.Flow.NodeLibrary;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Node.Llm;
using Kuroe.Workflows.Flows;
using Kuroe.Flow.InputChannel;
using Kuroe.Tests.Support;
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
        IReadOnlyList<NodeSpec> nodes = Members(AssistantSettings.DefaultLanguage);

        Assert.Equal(InputNodeTypes.Name, nodes[0].Kind);
        Assert.Equal(LlmNodeTypes.Name, nodes[1].Kind);
    }

    [Fact]
    public void Dialogue_flow_marks_the_reply_port_as_signal_on_both_sides()
    {
        IReadOnlyList<NodeSpec> nodes = Members(AssistantSettings.DefaultLanguage);

        Assert.Equal(PortType.Signal, nodes[0].Inputs.Find(DialogueFlow.ReplyPort)!.Value.Type);
        Assert.Equal(PortType.Signal, nodes[1].Outputs.Find(DialogueFlow.ReplyPort)!.Value.Type);
    }

    [Fact]
    public void Dialogue_reply_node_declares_constant_rule_as_system_prompt()
    {
        NodeSpec reply = Members(AssistantSettings.DefaultLanguage)[1];
        LlmParameters parameters = Assert.IsType<LlmParameters>(Assert.IsType<LlmNodeType>(reply.Type).Parameters);

        Assert.Equal(
            ["你是Kuroe的内置助手，直接回应用户的输入。回复内容写在正常输出里。", "使用「中文」语言母语者语言风格回答。"],
            parameters.SystemPrompt);
        Assert.Null(parameters.Prompt);
    }

    [Fact]
    public void Dialogue_reply_node_uses_the_configured_language()
    {
        NodeSpec reply = Members("English")[1];
        LlmParameters parameters = Assert.IsType<LlmParameters>(Assert.IsType<LlmNodeType>(reply.Type).Parameters);

        Assert.Equal(
            ["你是Kuroe的内置助手，直接回应用户的输入。回复内容写在正常输出里。", "使用「English」语言母语者语言风格回答。"],
            parameters.SystemPrompt);
    }

    /// <summary>回话节点的 Signal 输出端口同样按需交回端口放行端口交回工具。</summary>
    [Fact]
    public void Dialogue_reply_node_grants_the_port_contract_tool()
    {
        NodeSpec reply = Members(AssistantSettings.DefaultLanguage)[1];
        LlmNodeType type = Assert.IsType<LlmNodeType>(reply.Type);
        IReadOnlyList<string> paths = [.. type.ToolPaths(reply.Outputs).Select(path => path.Value)];

        Assert.Equal(DialogueFlow.Tools.Select(path => path.Value), paths.Take(DialogueFlow.Tools.Count));
        Assert.Equal(
            [ContractTools.Port, ContractTools.CannotComplete, ContractTools.Status],
            paths.Skip(DialogueFlow.Tools.Count));
    }

    [Fact]
    public void Dialogue_flow_whitelists_dialogue_tools_by_path()
    {
        Assert.Equal(
            [
                new ToolPath("GetLocalTime"),
                new ToolPath("KuroeInfo"),
                new ToolPath("file/read"),
                new ToolPath("source"),
                new ToolPath("git/read"),
                new ToolPath("dotnet/read"),
            ],
            DialogueFlow.Tools);
    }

    private static FlowDefinition Build(string language)
    {
        IFlowDescriptionCompiler compiler = new FlowDescriptionCompiler(
            TestTypeFiles.Registry(Inputs), new NodeLibraryExpander());

        return compiler.Compile(DialogueFlow.Build(language), []).ThrowIfError().Flows.Single().Model;
    }

    private static IReadOnlyList<NodeSpec> Members(string language) =>
        Build(language).RootNode.Nodes ?? throw new InvalidOperationException("对话流程缺少根节点成员。");
}

using Kuroe.Flow.Execution;
using Kuroe.Node.Input;
using Kuroe.Tool;
using Kuroe.Flow.Compilation;
using Kuroe.Flow.Compilation.Shared;
using Kuroe.Flow.NodeLibrary;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Kuroe.Node.Llm;
using Kuroe.Assistant.Flows;
using Kuroe.Flow.InputChannel;
using Kuroe.Shared;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Assistant.Tests;

/// <summary>内置对话流程的节点配置与工具白名单在工具面上的应用。</summary>
public sealed class DialogueTests
{
    private const string DefaultLanguage = "中文";

    /// <summary>外部输入协议的宿主实现，只为建出流程。</summary>
    private static readonly InputRegistry Inputs = new();

    [Fact]
    public void Dialogue_flow_builds_input_and_text_reply_nodes()
    {
        IReadOnlyList<NodeSpec> nodes = Members(DefaultLanguage);

        Assert.Equal(InputNodeTypes.Name, nodes[0].Kind);
        Assert.Equal(LlmNodeTypes.Name, nodes[1].Kind);
    }

    [Fact]
    public void Dialogue_flow_marks_the_reply_port_as_signal_on_both_sides()
    {
        IReadOnlyList<NodeSpec> nodes = Members(DefaultLanguage);

        Assert.Equal(PortTypes.Signal, nodes[0].Inputs.Single(port => port.Name == DialogueFlow.ReplyPort).Type);
        Assert.Equal(PortTypes.Signal, nodes[1].Outputs.Single(port => port.Name == DialogueFlow.ReplyPort).Type);
    }

    [Fact]
    public void Dialogue_reply_node_declares_constant_rule_as_system_prompt()
    {
        NodeSpec reply = Members(DefaultLanguage)[1];
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

    private static ExecutionOutputPort ToExecutionPort(Port port)
    {
        ExecutionPortKind kind = port.Type == PortTypes.Signal
            ? ExecutionPortKind.Signal
            : ExecutionPortKind.Text;
        if (port.Type == PortTypes.Context)
        {
            kind = ExecutionPortKind.Context;
        }

        return new ExecutionOutputPort(
            new ExecutionPortName(port.Name.Value),
            kind,
            false,
            port.Trigger,
            PortRules.RequiresSubmission(port.Type),
            port.Mode == PortMode.Constant);
    }

    /// <summary>回话节点的 Signal 输出端口同样按需交回端口放行端口交回工具。</summary>
    [Fact]
    public void Dialogue_reply_node_grants_the_port_contract_tool()
    {
        NodeSpec reply = Members(DefaultLanguage)[1];
        LlmNodeType type = Assert.IsType<LlmNodeType>(reply.Type);
        IReadOnlyList<string> paths =
            [.. type.ToolPaths([.. reply.Outputs.Select(ToExecutionPort)]).Select(path => path.Value)];

        Assert.Equal(DialogueFlow.Tools.Select(path => path.Value), paths.Take(DialogueFlow.Tools.Count));
        Assert.Equal(
            [ContractTools.Port, ContractTools.CannotComplete, ContractTools.Status],
            paths.Skip(DialogueFlow.Tools.Count));
    }

    /// <summary>回话节点的工作区静态端口由根容器的绑定从开始端口转接，节点用绑定取用。</summary>
    [Fact]
    public void Dialogue_reply_node_receives_the_workspace_port_from_the_start_node()
    {
        NodeSpec reply = Members(DefaultLanguage)[1];
        IReadOnlyDictionary<PortName, PortRef> bindings = reply.Bindings ?? new Dictionary<PortName, PortRef>();

        Assert.Contains(PortRef.Start(DialogueFlow.WorkspacePort), bindings.Values);
        Assert.True(reply.FindInput(DialogueFlow.WorkspacePort) is { IsStatic: true });
    }

    /// <summary>回话节点把工作区端口映射到带路径变量的工具根。</summary>
    [Fact]
    public void Dialogue_reply_node_maps_the_workspace_port_to_tool_roots()
    {
        NodeSpec reply = Members(DefaultLanguage)[1];
        LlmParameters parameters = Assert.IsType<LlmParameters>(Assert.IsType<LlmNodeType>(reply.Type).Parameters);

        Assert.Equal(DialogueFlow.WorkspacePort.Value, parameters.ToolPorts["file"][PathVariables.RootName]);
        Assert.Equal(DialogueFlow.WorkspacePort.Value, parameters.ToolPorts["git"][PathVariables.RootName]);
        Assert.Equal(DialogueFlow.WorkspacePort.Value, parameters.ToolPorts["dotnet"][PathVariables.RootName]);
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

    /// <summary>对话流程只声明开始端口，模型端口由宿主的开始输入表单给值，流程模型不带绑定。</summary>
    [Fact]
    public void Dialogue_model_port_is_declared_and_bound_by_the_host_form()
    {
        FlowDefinition flow = Build(DefaultLanguage);
        PortName model = LlmNodeTypes.ModelPort;

        Assert.Null(Assert.NotNull(flow.StartPorts.Find(model)).Value);
        Assert.Equal("MODEL", DialogueFlow.HostInputs[model]);
    }

    private static FlowDefinition Build(string language)
    {
        IFlowDescriptionCompiler compiler = new FlowDescriptionCompiler(
            TestTypeFiles.Registry(Inputs), new NodeLibraryExpander());

        var compiled = compiler.Compile(DialogueFlow.Build(language), []);
        if (compiled.IsError)
        {
            throw new InvalidOperationException(
                string.Join("；", compiled.ErrorsOrEmptyList.Select(error => error.Description)));
        }

        return compiled.Value.Flows.Single().Model;
    }

    private static IReadOnlyList<NodeSpec> Members(string language) =>
        Build(language).RootNode.Nodes ?? throw new InvalidOperationException("对话流程缺少根节点成员。");
}

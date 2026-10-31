using Kuroe.Flow.Compilation.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Kuroe.Node.Llm;
using Kuroe.Shared;
using Kuroe.Tool;
using Kuroe.Workflows.Tasks;

namespace Kuroe.Assistant.Flows;

/// <summary>内置对话流程：收话与回话互引构成环，收话由 开始 触发，一轮回答驱动一轮迭代后回到挂起。管理层用它创建常驻对话任务。</summary>
internal static class DialogueFlow
{
    /// <summary>流程名。</summary>
    internal static FlowName Name { get; } = new("对话");

    /// <summary>开始入口的输出端口名：任务的初始输入由此进入流程。</summary>
    internal static PortName StartPort { get; } = new("目标");

    /// <summary>收话节点的节点名，环的挂点。</summary>
    internal static NodeName InputNodeName { get; } = new("收话");

    /// <summary>收话节点的标签。</summary>
    internal static NodeLabel InputNodeLabel { get; } = new(InputNodeName.Value);

    /// <summary>回话节点的节点名，面向任务模型的一问一答。</summary>
    internal static NodeName ReplyNodeName { get; } = new("回话");

    /// <summary>回话节点的标签。</summary>
    internal static NodeLabel ReplyNodeLabel { get; } = new(ReplyNodeName.Value);

    /// <summary>收话节点回答写入的输出端口名。</summary>
    internal static PortName InputPort { get; } = new("回答");

    /// <summary>回复端口名：回话节点的输出端口，只作环的回边驱动，也是收话节点取用它的输入端口。</summary>
    internal static PortName ReplyPort { get; } = new("完成");

    /// <summary>工作区根目录端口，由宿主在任务启动时给出。</summary>
    internal static PortName WorkspacePort { get; } = new("工作区");

    /// <summary>回话节点可用的能力白名单，写上级路径即放行整棵子树。文件只读工具可用，路径变量绑定工作区。</summary>
    internal static IReadOnlyList<ToolPath> Tools { get; } =
    [
        new ToolPath("GetLocalTime"),
        new ToolPath("KuroeInfo"),
        new ToolPath("file/read"),
        new ToolPath("source"),
        new ToolPath("git/read"),
        new ToolPath("dotnet/read"),
    ];

    /// <summary>回话节点的固定身份说明。</summary>
    private const string Identity = "你是Kuroe的内置助手，直接回应用户的输入。回复内容写在正常输出里。";

    /// <summary>回话节点的恒定系统指令块：身份说明与回复语言。</summary>
    internal static IReadOnlyList<string> SystemPrompt(string language) =>
    [
        Identity,
        $"使用「{language}」语言母语者语言风格回答。",
    ];

    /// <summary>内置对话流程：回话使用宿主在对话任务启动时给出的模型。外部输入协议由类型描述提供，语言由宿主的助手配置给出。</summary>
    internal static FlowDocument Build(string language) => new(
        [],
        [
            new FlowDocumentFlow(
                Name.Value,
                "内置对话：回答用户的每轮输入",
                [
                    new PortDocument(StartPort.Value, PortTypes.Text, Description: "任务的初始输入，提交时给出。"),
                    new PortDocument(WorkspacePort.Value, PortTypes.Text, PortMode.Constant, "工作区根目录，由宿主给出。"),
                    new PortDocument(
                        LlmNodeTypes.ModelPort.Value,
                        PortTypes.Text,
                        PortMode.Constant,
                        "执行使用的模型名，由宿主在任务启动时给出。"),
                ],
                new Dictionary<string, string>
                {
                    [WorkspacePort.Value] = StartSources.Workspace,
                    [LlmNodeTypes.ModelPort.Value] = StartSources.Model,
                },
                new NodeDefinitionDocument
                {
                    Name = "对话",
                    Kind = "Container",
                    Inputs =
                    [
                        new InputPortDocument(StartPort.Value, PortTypes.Text),
                        new InputPortDocument(WorkspacePort.Value, PortTypes.Text, Mode: PortMode.Constant),
                        new InputPortDocument(LlmNodeTypes.ModelPort.Value, PortTypes.Text, Mode: PortMode.Constant),
                    ],
                    In = new Dictionary<string, string>
                    {
                        [StartPort.Value] = $"{StartNode.ReservedLabel.Value}@{StartPort.Value}",
                        [WorkspacePort.Value] = $"{StartNode.ReservedLabel.Value}@{WorkspacePort.Value}",
                        [LlmNodeTypes.ModelPort.Value] = $"{StartNode.ReservedLabel.Value}@{LlmNodeTypes.ModelPort.Value}",
                    },
                    Nodes =
                    [
                        new NodeDefinitionDocument
                        {
                            Name = InputNodeName.Value,
                            Kind = "Input",
                            Inputs =
                            [
                                new InputPortDocument(StartPort.Value, PortTypes.Text),
                                // 回复输入只作回边的触发，不消费回话产出，回复内容随回话执行的过程播发写出
                                new InputPortDocument(ReplyPort.Value, PortTypes.Signal),
                            ],
                            Outputs = [new PortDocument(InputPort.Value, PortTypes.Text)],
                            Parameters = new Dictionary<string, FlowValue>
                            {
                                ["Question"] = new FlowValue.Text("请输入"),
                            },
                            From =
                            [
                                new FromGroupDocument(
                                [
                                    new FromLinkDocument(StartPort.Value, $"@{StartPort.Value}"),
                                    new FromLinkDocument(ReplyPort.Value, $"{ReplyNodeLabel.Value}@{ReplyPort.Value}"),
                                ]),
                            ],
                        },
                        new NodeDefinitionDocument
                        {
                            Name = ReplyNodeName.Value,
                            Kind = "Llm",
                            Inputs =
                            [
                                new InputPortDocument(InputPort.Value, PortTypes.Text),
                                new InputPortDocument(WorkspacePort.Value, PortTypes.Text, Mode: PortMode.Constant),
                                new InputPortDocument(LlmNodeTypes.ModelPort.Value, PortTypes.Text, Mode: PortMode.Constant),
                            ],
                            Static = new Dictionary<string, string>
                            {
                                [LlmNodeTypes.ModelPort.Value] = $"@{LlmNodeTypes.ModelPort.Value}",
                                [WorkspacePort.Value] = $"@{WorkspacePort.Value}",
                            },
                            // 回复端口只作回边驱动，不承载内容，回复内容随回话执行的过程播发写出
                            Outputs = [new PortDocument(ReplyPort.Value, PortTypes.Signal)],
                            Parameters = new Dictionary<string, FlowValue>
                            {
                                ["Tools"] = new FlowValue.Array([.. Tools.Select(tool => new FlowValue.Text(tool.Value))]),
                                ["ToolPorts"] = ToolPorts(),
                                ["SystemPrompt"] = new FlowValue.Array([.. SystemPrompt(language).Select(text => new FlowValue.Text(text))]),
                            },
                            From =
                            [
                                new FromGroupDocument(
                                [
                                    new FromLinkDocument(InputPort.Value, $"{InputNodeLabel.Value}@{InputPort.Value}"),
                                ]),
                            ],
                        },
                    ],
                }),
        ]);

    /// <summary>工具路径到路径变量映射的对应表，子树按上级路径给出。</summary>
    private static FlowValue.Object ToolPorts() => new(new Dictionary<string, FlowValue>
    {
        ["file"] = RootPorts(),
        ["git"] = RootPorts(),
        ["dotnet"] = RootPorts(),
    });

    /// <summary>单个工具组的变量映射，root 指向工作区端口。</summary>
    private static FlowValue.Object RootPorts() => new(new Dictionary<string, FlowValue>
    {
        [PathVariables.RootName] = new FlowValue.Text(WorkspacePort.Value),
    });
}

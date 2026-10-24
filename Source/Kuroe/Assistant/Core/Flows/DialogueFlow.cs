using Kuroe.Flow.Compiler.Shared;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Tool;

namespace Kuroe.Assistant.Flows;

/// <summary>内置对话流程：收话与回话互引构成环，收话由 开始 触发，一轮回答驱动一轮迭代后回到挂起。管理层用它创建常驻对话任务。</summary>
internal static class DialogueFlow
{
    /// <summary>流程名。</summary>
    internal static FlowName Name { get; } = new("对话");

    /// <summary>开始入口的输出端口名：任务的初始输入由此进入流程。</summary>
    internal static PortName StartPort { get; } = new("目标");

    /// <summary>运行时模型选择名：执行时取宿主当前选中的模型。</summary>
    internal static ModelRef RuntimeModel { get; } = new("运行时");

    /// <summary>收话节点的节点名，环的挂点。</summary>
    internal static NodeName InputNodeName { get; } = new("收话");

    /// <summary>收话节点的标签。</summary>
    internal static NodeLabel InputNodeLabel { get; } = new(InputNodeName.Value);

    /// <summary>回话节点的节点名，面向运行时模型的一问一答。</summary>
    internal static NodeName ReplyNodeName { get; } = new("回话");

    /// <summary>回话节点的标签。</summary>
    internal static NodeLabel ReplyNodeLabel { get; } = new(ReplyNodeName.Value);

    /// <summary>收话节点回答写入的输出端口名。</summary>
    internal static PortName InputPort { get; } = new("回答");

    /// <summary>回复端口名：回话节点的输出端口，只作环的回边驱动，也是收话节点取用它的输入端口。</summary>
    internal static PortName ReplyPort { get; } = new("完成");

    /// <summary>回话节点可用的能力白名单，写上级路径即放行整棵子树。文件工具不在此列，对话任务没有工作目录。</summary>
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

    /// <summary>内置对话流程：回话面向运行时模型解析，随模型选择切换即时生效。外部输入协议由类型描述提供，语言由宿主的助手配置给出。</summary>
    internal static FlowDocument Build(string language) => new(
        [],
        [
            new FlowDocumentFlow(
                Name.Value,
                "内置对话：回答用户的每轮输入",
                [new ModelDocument(RuntimeModel.Value, null, Runtime: true)],
                [new PortDocument(StartPort.Value, PortTypes.Text, "任务的初始输入，提交时给出。")],
                new NodeDefinitionDocument
                {
                    Name = "对话",
                    Kind = "Container",
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
                                    new FromLinkDocument(StartPort.Value, $"{StartNode.ReservedLabel.Value}@{StartPort.Value}"),
                                    new FromLinkDocument(ReplyPort.Value, $"{ReplyNodeLabel.Value}@{ReplyPort.Value}"),
                                ]),
                            ],
                        },
                        new NodeDefinitionDocument
                        {
                            Name = ReplyNodeName.Value,
                            Kind = "Llm",
                            Inputs = [new InputPortDocument(InputPort.Value, PortTypes.Text)],
                            Model = RuntimeModel.Value,
                            // 回复端口只作回边驱动，不承载内容，回复内容随回话执行的过程播发写出
                            Outputs = [new PortDocument(ReplyPort.Value, PortTypes.Signal)],
                            Parameters = new Dictionary<string, FlowValue>
                            {
                                ["Tools"] = new FlowValue.Array([.. Tools.Select(tool => new FlowValue.Text(tool.Value))]),
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
}

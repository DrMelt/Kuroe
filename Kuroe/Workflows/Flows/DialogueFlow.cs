using Kuroe.Flow.Node.Input;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Workflows.Flows;

/// <summary>内置对话流程：收话与回话互引构成环，收话由 开始 触发，一轮回答驱动一轮迭代后回到挂起。管理层用它创建常驻对话任务。</summary>
internal static class DialogueFlow
{
    /// <summary>流程名。</summary>
    internal static FlowName Name { get; } = new("对话");

    /// <summary>开始入口的输出端口名：任务的初始输入由此进入流程。</summary>
    internal static PortName StartPort { get; } = new("目标");

    /// <summary>运行时模型选择名：执行时取宿主当前选中的模型。</summary>
    internal static ModelRef RuntimeModel { get; } = new("运行时");

    /// <summary>收话节点名：环的挂点。</summary>
    internal static NodeName InputNodeName { get; } = new("收话");

    /// <summary>回话节点名：面向运行时模型的一问一答。</summary>
    internal static NodeName ReplyNodeName { get; } = new("回话");

    /// <summary>收话节点回答写入的输出端口名。</summary>
    internal static PortName InputPort { get; } = new("回答");

    /// <summary>回复端口名：回话节点的输出端口，也是收话节点取用它的输入端口。</summary>
    internal static PortName ReplyPort { get; } = new("完成");

    /// <summary>回话节点可用的能力白名单，写上级路径即放行整棵子树。</summary>
    internal static IReadOnlyList<CapabilityPath> Tools { get; } =
    [
        new CapabilityPath("GetLocalTime"),
        new CapabilityPath("info"),
        new CapabilityPath("files"),
        new CapabilityPath("source"),
    ];

    /// <summary>回话节点的恒定系统指令块。</summary>
    internal static IReadOnlyList<string> SystemPrompt { get; } = ["你是Kuroe的内置助手，直接回应用户的输入。回复内容写在正常输出里，端口交回按本轮接口说明执行。"];

    /// <summary>内置对话流程：回话面向运行时模型解析，随用户 /model 切换即时生效。</summary>
    internal static FlowDefinition Build() => new(
        Name,
        "内置对话：回答用户的每轮输入",
        [new ModelDefinition { Name = RuntimeModel, Runtime = true }],
        [new Port(StartPort, PortType.Text, "任务的初始输入，提交时给出。")],
        new NodeSpec
        {
            Name = new NodeName("对话"),
            Kind = NodeTypes.Container,
            Nodes =
            [
                new NodeSpec
                {
                    Name = InputNodeName,
                    Kind = InputNodeTypes.Name,
                    Inputs =
                    [
                        new InputPort(StartPort, PortType.Text),
                        // 回复输入只作回边的触发，不消费回话产出，回复内容由 run 结果交给调用方
                        new InputPort(ReplyPort, PortType.Signal),
                    ],
                    Execution = new ExecutableSpec
                    {
                        Question = "请输入",
                    },
                    Outputs = [new Port(InputPort, PortType.Text)],
                    From =
                    [
                        new FromGroup(
                        [
                            new FromLink(StartPort, PortRef.Named(StartNode.ReservedName, StartPort)),
                            new FromLink(ReplyPort, PortRef.Named(ReplyNodeName, ReplyPort)),
                        ]),
                    ],
                },
                new NodeSpec
                {
                    Name = ReplyNodeName,
                    Kind = NodeTypes.Executable,
                    Inputs = [new InputPort(InputPort, PortType.Text)],
                    SystemPrompt = SystemPrompt,
                    Execution = new ExecutableSpec
                    {
                        Tools = Tools,
                    },
                    Model = RuntimeModel,
                    Outputs = [new Port(ReplyPort, PortType.Text)],
                    From = [new FromGroup([new FromLink(InputPort, PortRef.Named(InputNodeName, InputPort))])],
                },
            ],
        });
}

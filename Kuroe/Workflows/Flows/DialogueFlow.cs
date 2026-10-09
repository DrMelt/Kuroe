using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows;

/// <summary>内置对话流程：收话与回话互引构成环，输入节点是挂点，一轮回答驱动一轮迭代后回到挂起。管理层用它创建常驻对话任务。</summary>
internal static class DialogueFlow
{
    /// <summary>流程名。</summary>
    internal static FlowName Name { get; } = new("对话");

    /// <summary>运行时模型选择名：执行时取宿主当前选中的模型。</summary>
    internal static ModelRef RuntimeModel { get; } = new("运行时");

    /// <summary>收话节点名：环的挂点。</summary>
    internal static NodeName InputNodeName { get; } = new("收话");

    /// <summary>回话节点名：面向运行时模型的一问一答。</summary>
    internal static NodeName ReplyNodeName { get; } = new("回话");

    /// <summary>回话节点可用的能力工具白名单，写上级路径即放行整棵子树。</summary>
    internal static IReadOnlyList<ToolPath> Tools { get; } =
    [
        new ToolPath("GetLocalTime"),
        new ToolPath("info"),
        new ToolPath("files"),
    ];

    /// <summary>回话节点指令。</summary>
    internal static string Prompt { get; } = "直接回应用户的输入。";

    /// <summary>内置对话流程：回话面向运行时模型解析，随用户 /model 切换即时生效。</summary>
    internal static FlowDefinition Build() => new(
        Name,
        "内置对话：回答用户的每轮输入",
        [new ModelDefinition { Name = RuntimeModel, Runtime = true }],
        new NodeSpec
        {
            Name = new NodeName("对话"),
            Nodes =
            [
                new NodeSpec
                {
                    Name = InputNodeName,
                    Execution = new ExecutableSpec
                    {
                        Output = NodeOutput.Input,
                        Question = "请输入",
                    },
                    From = [ReplyNodeName],
                },
                new NodeSpec
                {
                    Name = ReplyNodeName,
                    Execution = new ExecutableSpec
                    {
                        Output = NodeOutput.Text,
                        Tools = Tools,
                        Prompt = Prompt,
                    },
                    Model = RuntimeModel,
                    From = [InputNodeName],
                },
            ],
        });
}

using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Executions;

/// <summary>一次执行的上下文。派出的执行只用这里声明的内容，不继承任何未声明的历史。</summary>
public sealed record RunContext
{
    /// <summary>所属任务。</summary>
    public required TaskId Task { get; init; }

    /// <summary>产出的契约，决定能交回什么。</summary>
    public required NodeOutput Output { get; init; }

    /// <summary>所属执行节点在流程图里的序号。</summary>
    public required int NodeIndex { get; init; }

    /// <summary>所属执行节点的名称。</summary>
    public required NodeName NodeName { get; init; }

    /// <summary>本次执行要做的事，作为一条用户消息发给模型。</summary>
    public required string Instruction { get; init; }

    /// <summary>派生时锁定的模型，运行期间不随选择变化。</summary>
    public required string Model { get; init; }

    /// <summary>所属条目序号，非按条目展开时为空。</summary>
    public int? ItemIndex { get; init; }

    /// <summary>本次执行被叫到的名字：执行节点名，展开条目时带上条目号。</summary>
    public string Label => ItemIndex is { } index ? $"{NodeName}·条目 {index + 1}" : NodeName.ToString();

    /// <summary>当前节点下本实例的执行次数，按节点与条目各自累计，从 1 起。</summary>
    public int ExecutionCount { get; init; } = 1;

    /// <summary>本轮可用的工具名单：节点声明的能力工具加契约工具。</summary>
    public IReadOnlyList<ToolName> Tools { get; init; } = [];

    /// <summary>上游装配进来的已有内容，按序置于指令之前。</summary>
    public IReadOnlyList<ContextMessage> Seed { get; init; } = [];
}

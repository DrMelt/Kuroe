
using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Agent;

/// <summary>agent 的执行上下文。派生出的 agent 只用这里声明的内容，不继承任何未声明的历史。</summary>
public sealed record RunContext
{
    /// <summary>所属任务。</summary>
    public required TaskId Task { get; init; }

    /// <summary>产出的契约，决定能交回什么。</summary>
    public required NodeOutput Output { get; init; }

    /// <summary>所属叶子在流程图里的序号。</summary>
    public required int NodeIndex { get; init; }

    /// <summary>所属叶子的名称。</summary>
    public required string NodeName { get; init; }

    /// <summary>本 agent 要做的事，作为一条用户消息发给模型。</summary>
    public required string Instruction { get; init; }

    /// <summary>派生时锁定的模型，运行期间不随选择变化。</summary>
    public required string Model { get; init; }

    /// <summary>agent 定义级的系统提示词，未声明时为 null 而用全局设置。</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>所属条目序号，非按条目展开的叶为空。</summary>
    public int? ItemIndex { get; init; }

    /// <summary>该 agent 被叫到的名字：叶子名，展开条目时带上条目号。</summary>
    public string Label => ItemIndex is { } index ? $"{NodeName}·条目 {index + 1}" : NodeName;

    /// <summary>本轮的检查或实施轮次，从 1 起。</summary>
    public int Attempt { get; init; } = 1;

    /// <summary>本轮可用的工具名单：能力工具加契约工具，执行期由叶子确定。</summary>
    public IReadOnlyList<string> Tools { get; init; } = [];

    /// <summary>上游装配进来的已有内容，按序置于指令之前。</summary>
    public IReadOnlyList<ContextMessage> Seed { get; init; } = [];
}

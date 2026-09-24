
namespace Kuroe.Shared.Agent;

/// <summary>agent 的执行上下文。派生出的 agent 只用这里声明的内容，不继承任何未声明的历史。</summary>
public sealed record RunContext
{
    /// <summary>所属任务。</summary>
    public required TaskId Task { get; init; }

    /// <summary>承担的角色。</summary>
    public required RunRole Role { get; init; }

    /// <summary>所属流程步骤的序号。</summary>
    public required int StepIndex { get; init; }

    /// <summary>所属流程步骤的名称。</summary>
    public required string StepName { get; init; }

    /// <summary>本 agent 要做的事，作为一条用户消息发给模型。</summary>
    public required string Instruction { get; init; }

    /// <summary>派生时锁定的模型，运行期间不随选择变化。</summary>
    public required string Model { get; init; }

    /// <summary>所属条目序号，非按条目展开的步骤为空。</summary>
    public int? ItemIndex { get; init; }

    /// <summary>该 agent 被叫到的名字：步骤名，展开条目时带上条目号。</summary>
    public string Label => ItemIndex is { } index ? $"{StepName}·条目 {index + 1}" : StepName;

    /// <summary>同一条目的第几轮实施，从 1 起。</summary>
    public int Attempt { get; init; } = 1;

    /// <summary>上游装配进来的已有内容，按序置于指令之前。</summary>
    public IReadOnlyList<ContextMessage> Seed { get; init; } = [];
}

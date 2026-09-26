namespace Kuroe.Shared.Workflows.Flows;

/// <summary>叶子下方的一段会话能力：系统提示词、模型与可用工具。叶子按名引用。</summary>
public sealed record AgentDefinition
{
    /// <summary>agent 名，流程内唯一。</summary>
    public required string Name { get; init; }

    /// <summary>给使用者的说明。</summary>
    public string? Description { get; init; }

    /// <summary>系统提示词，未写时用全局设置。</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>使用的模型，未写时用提交任务时选中的模型。</summary>
    public string? Model { get; init; }

    /// <summary>能力工具白名单，按函数名匹配。未写或空时不给出任何能力工具。</summary>
    public IReadOnlyList<string> Tools { get; init; } = [];
}
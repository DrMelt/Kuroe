namespace Kuroe.Shared.Workflows.Flows;

/// <summary>流程里的一个节点：叶子或容器。节点是流程的组织单元，
/// 统一面向作用域引用与上下文装配，对外只暴露名字与输入来源。</summary>
public abstract record NodeSpec
{
    /// <summary>节点名，流程内唯一。</summary>
    public required string Name { get; init; }

    /// <summary>对执行 agent 的额外要求，与目标一起构成指令。</summary>
    public string? Prompt { get; init; }

    /// <summary>上下文取自哪些更早节点的产出，引用规则由 WorkflowRules 校验。</summary>
    public IReadOnlyList<string> From { get; init; } = [];
}
namespace Kuroe.Shared.Workflows.Flows;

/// <summary>流程里的一个节点：执行节点或容器。节点功能声明落在统一基类上，两类节点共享。</summary>
public abstract record NodeSpec
{
    /// <summary>节点名，流程内唯一。</summary>
    public required string Name { get; init; }

    /// <summary>对执行单元的额外要求，与目标一起构成指令。</summary>
    public string? Prompt { get; init; }

    /// <summary>上下文取自哪些更早节点的产出，引用规则由 WorkflowRules 校验。</summary>
    public IReadOnlyList<string> From { get; init; } = [];

    /// <summary>节点进展到待批点时是否停人等批准：执行节点产出后，容器成员产出齐备后。</summary>
    public NodeGate Gate { get; init; } = NodeGate.Auto;
}
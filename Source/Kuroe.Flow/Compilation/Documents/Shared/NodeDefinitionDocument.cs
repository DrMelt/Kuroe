namespace Kuroe.Flow.Compilation.Documents.Shared;

/// <summary>定义节点：节点库定义、流程内联定义与容器。所有定义共用统一输出端口表。</summary>
public sealed record NodeDefinitionDocument : NodeDocument
{
    /// <summary>节点名，定义在库里的名字。</summary>
    public required string Name { get; init; }

    /// <summary>节点类型名。</summary>
    public required string Kind { get; init; }

    /// <summary>输入端口声明。</summary>
    public IReadOnlyList<InputPortDocument> Inputs { get; init; } = [];

    /// <summary>输出端口声明，容器按 Ref 写成员来源。</summary>
    public IReadOnlyList<PortDocument> Outputs { get; init; } = [];

    /// <summary>子节点，非空即容器。</summary>
    public IReadOnlyList<NodeDocument>? Nodes { get; init; }

    /// <summary>类型参数段：类型专属字段只在这里出现，容器与引擎结构字段都不进入。</summary>
    public NodeParameters Parameters { get; init; } = NodeParameters.Empty;
}
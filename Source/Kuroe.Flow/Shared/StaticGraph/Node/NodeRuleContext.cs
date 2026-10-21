namespace Kuroe.Flow.Shared.StaticGraph.Node;

/// <summary>节点规则的作用域：库定义、流程定义与引用实例。文案随作用域变化，判断只写一次。</summary>
public enum NodeRuleContext
{
    /// <summary>节点库定义。</summary>
    LibraryDefinition,

    /// <summary>流程里的定义节点与内联容器。</summary>
    FlowDefinition,

    /// <summary>引用节点库定义的实例。</summary>
    Reference,
}
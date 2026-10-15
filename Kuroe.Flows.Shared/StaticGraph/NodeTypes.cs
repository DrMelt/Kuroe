namespace Kuroe.Flows.Shared.StaticGraph;

/// <summary>引擎内置的节点类型名。执行与容器由引擎注册，其余类型由各自的扩展注册。</summary>
public static class NodeTypes
{
    /// <summary>容器：组织成员节点并限定作用域，自身不执行，成员齐备即汇合。</summary>
    public static NodeTypeName Container { get; } = new("Container");

    /// <summary>执行节点：由模型执行，收口时交回端口输出。</summary>
    public static NodeTypeName Executable { get; } = new("Executable");
}

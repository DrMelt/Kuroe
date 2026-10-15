namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>引擎内置的节点类型名。容器由引擎注册，其余类型由各自的扩展注册。</summary>
public static class BuiltinNodeKinds
{
    /// <summary>容器：组织成员节点并限定作用域，自身不执行，成员齐备即汇合。</summary>
    public static NodeKind Container { get; } = new("Container");
}

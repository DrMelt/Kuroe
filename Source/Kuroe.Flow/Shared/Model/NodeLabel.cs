namespace Kuroe.Flow.Shared.Model;

/// <summary>流程图里的节点标签：节点在图里的标识段。定义条目由节点名给出，引用条目由使用处写下，同一容器内唯一，展示即原文。</summary>
public readonly record struct NodeLabel(string Value)
{
    /// <summary>流程起点的保留节点标签。</summary>
    public static NodeLabel ReservedStart { get; } = new("Start");

    /// <summary>节点标签的文本形式。</summary>
    public override string ToString() => Value;
}
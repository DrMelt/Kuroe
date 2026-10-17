namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>节点库里的节点名。定义条目的名字，库内唯一，也是 Use 引用的名字。</summary>
public readonly record struct NodeName(string Value)
{
    /// <summary>节点名的文本形式。</summary>
    public override string ToString() => Value;
}
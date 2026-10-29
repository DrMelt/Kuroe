namespace Kuroe.Flow.Shared.Model;

/// <summary>节点库里的节点名。定义条目的名字，库内唯一，也是 Use 引用的名字。</summary>
public readonly record struct NodeName(string Value)
{
    /// <summary>流程起点的保留节点名。</summary>
    public static NodeName ReservedStart { get; } = new("Start");

    /// <summary>节点名的文本形式。</summary>
    public override string ToString() => Value;
}
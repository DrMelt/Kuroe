namespace Kuroe.Flow.Shared.StaticGraph.Nodes;

/// <summary>节点类型提供给信息查询的一条属性。</summary>
/// <param name="Name">属性名。</param>
/// <param name="Value">属性值。</param>
public sealed record NodeAttribute(string Name, string Value);
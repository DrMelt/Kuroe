namespace Kuroe.Flows.Shared.StaticGraph;

/// <summary>执行节点整节点一个执行还是按条目各派一个。</summary>
public enum NodeMode
{
    /// <summary>整节点一个执行。</summary>
    Single,

    /// <summary>规划交回的每个条目各一个执行。</summary>
    PerItem,
}

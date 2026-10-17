using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Tests.Support;

/// <summary>测试按节点名点名节点：执行视图里按名取节点路径，名字在流程里唯一时命中。</summary>
internal static class NodePaths
{
    /// <summary>按节点名取节点路径。</summary>
    public static NodePath PathOf(this INodeGraph graph, string name) =>
        graph.Nodes.FirstOrDefault(node => node.Name.Value == name)?.Path
        ?? throw new InvalidOperationException($"流程里没有名为 {name} 的节点。");
}
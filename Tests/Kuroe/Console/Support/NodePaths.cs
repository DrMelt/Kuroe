using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Console.Tests.Support;

/// <summary>测试按节点标签点名节点：执行视图里按标签取节点路径，标签在流程里唯一时命中。</summary>
internal static class NodePaths
{
    /// <summary>按节点标签取节点路径。</summary>
    public static NodePath PathOf(this INodeGraph graph, string label) =>
        graph.Nodes.FirstOrDefault(node => node.Label.Value == label)?.Path
        ?? throw new InvalidOperationException($"流程里没有标签为 {label} 的节点。");
}
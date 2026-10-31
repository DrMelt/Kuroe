using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Shared.StaticGraph.Graph;

/// <summary>流程的唯一来源节点：不属于任何容器，任务启动时即已放行。
/// 产出是任务启动时确定的开始端口值，来源是固定值、宿主输入或提交输入。
/// 动态端口经 From 引用 Start@端口 取用，常量端口经 Static 读取。</summary>
public sealed record StartNode : GraphNode
{
    /// <summary>保留的节点名，节点库定义的名字不得与之相同。</summary>
    public static NodeName ReservedName => NodeName.ReservedStart;

    /// <summary>保留的节点标签，流程内的节点标签不得与之相同。</summary>
    public static NodeLabel ReservedLabel => NodeLabel.ReservedStart;

    /// <summary>输出端口表：至少一个 Text 端口，不带值的端口至少一个。常量端口在任务启动时确定，只供 Static 读取。</summary>
    public override IReadOnlyList<Port> Outputs { get; init; } = [];
}

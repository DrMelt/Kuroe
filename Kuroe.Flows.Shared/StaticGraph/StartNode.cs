namespace Kuroe.Flows.Shared.StaticGraph;

/// <summary>流程的唯一来源节点：序号 0，不属于任何容器，任务启动时即已放行。
/// 产出是提交任务时给的初始输入，节点经 From 引用 Start@端口 取用，容器外也能引用。</summary>
public sealed record StartNode : GraphNode
{
    /// <summary>保留名，流程内的节点名与节点库定义名不得与之相同。</summary>
    public static NodeName ReservedName { get; } = new("Start");

    /// <summary>输出端口表：至少一个 Text 端口，不带值的端口恰好一个，输出提交任务的初始输入，其余端口带固定值。</summary>
    public required IReadOnlyList<Port> Outputs { get; init; }

    /// <summary>按名字取端口声明，未声明时为空。</summary>
    public Port? FindPort(PortName name) => Outputs.Find(name);
}

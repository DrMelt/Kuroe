using Kuroe.Flow.Presentation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;

namespace Kuroe.Flow.Shared.RuntimeGraph.Nodes;

/// <summary>一个容器节点在某时刻的执行状态：内容齐备情况、成员活动、发布次数、输出端口与常量输出文本。</summary>
public sealed record ContainerSnapshot(
    NodePath Path,
    NodeState State,
    IReadOnlyList<NodePath> Children,
    IPortTexts ConstantTexts,
    int PublicationCount,
    int MaxExecutions)
{
    /// <summary>容器的输出端口。</summary>
    public IReadOnlyList<FlowPortView> Outputs { get; init; } = [];
}

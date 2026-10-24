using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Shared.StaticGraph.Graph;

/// <summary>消费账的键：来源节点与来源端口。节点级绑定没有端口，容器按节点整体记账。</summary>
public readonly record struct ConsumptionKey(NodePath Source, PortName? Port);
using Kuroe.Flow.Shared.Model;

namespace Kuroe.Flow.Shared.StaticGraph.Graph;

/// <summary>已消费版本的键：来源节点与来源端口。来源节点为空表示流程开始端口，节点级绑定没有端口。</summary>
public readonly record struct ConsumptionKey(NodePath? Source, PortName? Port);
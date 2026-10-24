namespace Kuroe.Flow.Shared.StaticGraph.PortModel;

/// <summary>一次端口类型注册产生的两个类型：普通版与 Trigger 版。</summary>
public readonly record struct PortTypePair(PortType Plain, PortType Trigger);
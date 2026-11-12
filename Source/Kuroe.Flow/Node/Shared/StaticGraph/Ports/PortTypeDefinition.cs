namespace Kuroe.Flow.Shared.StaticGraph.Ports;

/// <summary>端口类型注册项：类型身份、未写模式时的默认模式与允许的模式集合。</summary>
public sealed record PortTypeDefinition(
    PortType Type,
    PortMode DefaultMode,
    IReadOnlySet<PortMode> Modes);

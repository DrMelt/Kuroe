using Kuroe.Flow.Graph.Shared.StaticGraph.Ports;

namespace Kuroe.Document.Shared;

/// <summary>输入端口条目：执行节点与容器共用。Mode 省略时按类型默认模式，Constant 表示静态输入，值由 Bindings 绑定或默认值提供。
/// Role 是执行角色。</summary>
public sealed record InputPortDocument(
    string Name,
    PortType? Type = null,
    PortMode? Mode = null,
    string? Description = null,
    string? Default = null,
    InputRole? Role = null);
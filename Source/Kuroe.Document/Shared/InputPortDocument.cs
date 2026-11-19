namespace Kuroe.Document.Shared;

/// <summary>输入端口条目：执行节点与容器共用。Type 是已注册的端口类型名。Mode 省略时按类型默认模式，Constant 表示静态输入，值由 Bindings 绑定或默认值提供。
/// Role 是执行角色。</summary>
public sealed record InputPortDocument(
    string Name,
    string? Type = null,
    PortModeDocument? Mode = null,
    string? Description = null,
    string? Default = null,
    InputRoleDocument? Role = null);
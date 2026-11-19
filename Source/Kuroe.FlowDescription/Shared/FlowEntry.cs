namespace Kuroe.FlowDescription.Shared;

/// <summary>流程描述里的一条流程：流程级开始端口与装配树。宿主输入绑定不进入流程描述，由宿主单独维护。</summary>
public sealed record FlowEntry(
    string Name,
    string? Description,
    IReadOnlyList<PortDescription> StartPorts,
    NodeDescription? Root);
namespace Kuroe.Flow.Compilation.Documents.Shared;

/// <summary>描述文档里的一条流程：流程级开始端口与装配树。宿主输入绑定不进入流程文档，由宿主单独维护。</summary>
public sealed record FlowDocumentFlow(
    string Name,
    string? Description,
    IReadOnlyList<PortDocument> StartPorts,
    NodeDocument? Root);
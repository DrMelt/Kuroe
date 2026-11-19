using Kuroe.Flow.Graph.Shared.StaticGraph.Ports;

namespace Kuroe.Document.Shared;

/// <summary>上游连接：目标输入端口、来源引用与触发门控条件。条件未写时按正向。</summary>
public sealed record FromLinkDocument(
    string Input,
    string Ref,
    TriggerCondition? Condition = null);